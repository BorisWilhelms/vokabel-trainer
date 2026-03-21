namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;

public class LeitnerServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void GetInterval_ReturnsCorrectValue(int box, int expectedInterval)
    {
        LeitnerService.GetInterval(box).Should().Be(expectedInterval);
    }

    [Fact]
    public void PromoteBox_IncrementsAndSetsInterval()
    {
        var entry = new BoxEntry { Box = 2, SessionsUntilReview = 0 };
        LeitnerService.Promote(entry);
        entry.Box.Should().Be(3);
        entry.SessionsUntilReview.Should().Be(4);
    }

    [Fact]
    public void PromoteBox_AtMax_StaysAtFive()
    {
        var entry = new BoxEntry { Box = 5, SessionsUntilReview = 0 };
        LeitnerService.Promote(entry);
        entry.Box.Should().Be(5);
        entry.SessionsUntilReview.Should().Be(16);
    }

    [Fact]
    public void DemoteBox_ResetsToOne()
    {
        var entry = new BoxEntry { Box = 4, SessionsUntilReview = 5 };
        LeitnerService.Demote(entry);
        entry.Box.Should().Be(1);
        entry.SessionsUntilReview.Should().Be(1);
    }

    [Fact]
    public async Task EnsureBoxEntries_CreatesForNewVocabulary()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = Api.Models.UserRole.User };
        var lang = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        list.Vocabularies.Add(new Vocabulary { Term = "res", Translations = "[\"Sache\"]" });
        list.Vocabularies.Add(new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" });
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        var service = new LeitnerService(db);
        await service.EnsureBoxEntriesAsync(user.Id, list.Id);

        db.BoxEntries.Should().HaveCount(2);
        db.BoxEntries.Should().AllSatisfy(b =>
        {
            b.Box.Should().Be(1);
            b.SessionsUntilReview.Should().Be(0);
        });
    }

    [Fact]
    public async Task DecrementSessionCounters_ReducesAll()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = Api.Models.UserRole.User };
        var lang = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        var vocab1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        var vocab2 = new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" };
        list.Vocabularies.AddRange([vocab1, vocab2]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab1.Id, Box = 3, SessionsUntilReview = 3 });
        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab2.Id, Box = 2, SessionsUntilReview = 1 });
        await db.SaveChangesAsync();

        var service = new LeitnerService(db);
        await service.DecrementSessionCountersAsync(user.Id, list.Id);

        db.BoxEntries.First(b => b.VocabularyId == vocab1.Id).SessionsUntilReview.Should().Be(2);
        db.BoxEntries.First(b => b.VocabularyId == vocab2.Id).SessionsUntilReview.Should().Be(0);
    }
}
