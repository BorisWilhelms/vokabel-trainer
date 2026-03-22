namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;

public class LeitnerService(AppDbContext db)
{
    private static readonly Dictionary<int, int> Intervals = new()
    {
        { 1, 1 }, { 2, 2 }, { 3, 4 }, { 4, 8 }, { 5, 16 }
    };

    public static int GetInterval(int box) => Intervals[box];

    public static void Promote(BoxEntry entry)
    {
        entry.Box = Math.Min(entry.Box + 1, 5);
        entry.SessionsUntilReview = GetInterval(entry.Box);
    }

    public static void Demote(BoxEntry entry)
    {
        entry.Box = 1;
        entry.SessionsUntilReview = GetInterval(1);
    }

    public async Task EnsureBoxEntriesAsync(int userId, int listId)
    {
        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var existingVocabIds = await db.BoxEntries
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .Select(b => b.VocabularyId)
            .ToListAsync();

        var newEntries = vocabIds
            .Except(existingVocabIds)
            .Select(vid => new BoxEntry
            {
                UserId = userId,
                VocabularyId = vid,
                Box = 1,
                SessionsUntilReview = 0
            });

        db.BoxEntries.AddRange(newEntries);
        await db.SaveChangesAsync();
    }

    public async Task DecrementSessionCountersAsync(int userId, int listId)
    {
        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var entries = await db.BoxEntries
            .AsTracking()
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .ToListAsync();

        foreach (var entry in entries)
            entry.SessionsUntilReview = Math.Max(0, entry.SessionsUntilReview - 1);

        await db.SaveChangesAsync();
    }
}
