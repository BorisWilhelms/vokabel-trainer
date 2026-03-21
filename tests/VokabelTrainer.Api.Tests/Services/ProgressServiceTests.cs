// tests/Services/ProgressServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using System.Text.Json;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Api.Models;

public class ProgressServiceTests
{
    [Fact]
    public async Task GetListProgress_ReturnsBoxDistribution()
    {
        using var db = TestDbContextFactory.Create();
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
        var v1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        var v2 = new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" };
        list.Vocabularies.AddRange([v1, v2]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v1.Id, Box = 1, SessionsUntilReview = 0 });
        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v2.Id, Box = 3, SessionsUntilReview = 2 });
        await db.SaveChangesAsync();

        var service = new ProgressService(db);
        var result = await service.GetListProgressAsync(user.Id, list.Id);

        result.Should().NotBeNull();
        result!.BoxDistribution.Box1.Should().Be(1);
        result.BoxDistribution.Box3.Should().Be(1);
    }

    [Fact]
    public async Task GetListProgress_ReturnsProblemVocabulary()
    {
        using var db = TestDbContextFactory.Create();
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
        var v1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        list.Vocabularies.Add(v1);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v1.Id, Box = 1, SessionsUntilReview = 0 });

        var session = new TrainingSession
        {
            UserId = user.Id, ListId = list.Id,
            Mode = TrainingMode.SinglePass, StartedAt = DateTime.UtcNow
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();

        // 3 wrong answers
        for (int i = 0; i < 3; i++)
            db.TrainingAnswers.Add(new TrainingAnswer
            {
                SessionId = session.Id, VocabularyId = v1.Id,
                Direction = Direction.SourceToTarget, GivenAnswer = "falsch",
                IsCorrect = false, AnsweredAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new ProgressService(db);
        var result = await service.GetListProgressAsync(user.Id, list.Id);

        result!.ProblemVocabulary.Should().HaveCount(1);
        result.ProblemVocabulary[0].Term.Should().Be("res");
        result.ProblemVocabulary[0].TimesWrong.Should().Be(3);
    }
}
