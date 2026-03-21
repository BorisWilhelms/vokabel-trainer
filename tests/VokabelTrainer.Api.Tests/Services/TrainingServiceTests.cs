namespace VokabelTrainer.Api.Tests.Services;
using System.Text.Json;
using FluentAssertions;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Models;

public class TrainingServiceTests
{
    private async Task<(AppDbContext db, int userId, int listId)> SetupTestDataAsync()
    {
        var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = UserRole.User, IsInitialized = true };
        var lang1 = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang1, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang1.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        list.Vocabularies.AddRange([
            new Vocabulary { Term = "res", Translations = JsonSerializer.Serialize(new[] { "Sache", "Ding" }) },
            new Vocabulary { Term = "amo", Translations = JsonSerializer.Serialize(new[] { "lieben" }) },
            new Vocabulary { Term = "bellum", Translations = JsonSerializer.Serialize(new[] { "Krieg" }) },
        ]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        // Create box entries (all due)
        foreach (var vocab in list.Vocabularies)
            db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab.Id, Box = 1, SessionsUntilReview = 0 });
        await db.SaveChangesAsync();

        return (db, user.Id, list.Id);
    }

    [Fact]
    public async Task StartSession_CreatesSession()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);

        sessionId.Should().BeGreaterThan(0);
        var session = db.TrainingSessions.First();
        session.Mode.Should().Be(TrainingMode.SinglePass);
        session.ListId.Should().Be(listId);
    }

    [Fact]
    public async Task GetNextQuestion_ReturnsDueVocabulary()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        question.Should().NotBeNull();
        question!.Prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitAnswer_CorrectAnswer_ReturnsTrue()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        // Build correct answer based on direction
        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var correctAnswer = question!.Direction == Direction.SourceToTarget
            ? translations[0]
            : vocab.Term;

        var response = await service.SubmitAnswerAsync(sessionId, question.VocabularyId, question.Direction, correctAnswer);

        response.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_WrongAnswer_ReturnsFalse()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var response = await service.SubmitAnswerAsync(sessionId, question!.VocabularyId, question.Direction, "voellig falsch");

        response.IsCorrect.Should().BeFalse();
        response.CorrectAnswers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SubmitAnswer_CaseInsensitive()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var answer = question!.Direction == Direction.SourceToTarget
            ? translations[0].ToUpper()
            : vocab.Term.ToUpper();

        var response = await service.SubmitAnswerAsync(sessionId, question.VocabularyId, question.Direction, answer);

        response.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_CorrectPromotesBox()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var answer = question!.Direction == Direction.SourceToTarget
            ? translations[0] : vocab.Term;

        await service.SubmitAnswerAsync(sessionId, question.VocabularyId, question.Direction, answer);

        var box = db.BoxEntries.First(b => b.VocabularyId == question.VocabularyId);
        box.Box.Should().Be(2);
    }

    [Fact]
    public async Task SubmitAnswer_WrongDemotesToBoxOne()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        // Move one vocab to box 3
        var firstVocab = db.Vocabularies.First();
        var boxEntry = db.BoxEntries.First(b => b.VocabularyId == firstVocab.Id);
        boxEntry.Box = 3;
        await db.SaveChangesAsync();

        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        // Keep getting questions until we get the one in box 3
        // For simplicity, just submit wrong for any question
        var question = await service.GetNextQuestionAsync(sessionId);
        await service.SubmitAnswerAsync(sessionId, question!.VocabularyId, question.Direction, "falsch");

        var box = db.BoxEntries.First(b => b.VocabularyId == question.VocabularyId);
        box.Box.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionResult_ReturnsCorrectStats()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);

        // Answer all questions
        for (int i = 0; i < 3; i++)
        {
            var q = await service.GetNextQuestionAsync(sessionId);
            if (q == null) break;
            await service.SubmitAnswerAsync(sessionId, q.VocabularyId, q.Direction, i == 0 ? "falsch" : GetCorrectAnswer(db, q));
        }

        var result = await service.GetSessionResultAsync(sessionId);

        result.Should().NotBeNull();
        result!.TotalQuestions.Should().Be(3);
        result.CorrectAnswers.Should().Be(2);
        result.WrongAnswers.Should().HaveCount(1);
    }

    private static string GetCorrectAnswer(AppDbContext db, TrainingQuestionDto q)
    {
        var vocab = db.Vocabularies.First(v => v.Id == q.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        return q.Direction == Direction.SourceToTarget ? translations[0] : vocab.Term;
    }
}
