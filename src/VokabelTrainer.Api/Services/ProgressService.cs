// Services/ProgressService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Models.Lists;
using VokabelTrainer.Api.Models.Progress;

public class ProgressService(AppDbContext db)
{
    public async Task<ListProgressDto?> GetListProgressAsync(int userId, int listId)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == listId && l.UserId == userId)
            .FirstOrDefaultAsync();

        if (list is null) return null;

        var boxCounts = await db.BoxEntries
            .Where(b => b.UserId == userId && b.Vocabulary.ListId == listId)
            .GroupBy(b => b.Box)
            .Select(g => new { Box = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Box, x => x.Count);

        var boxDist = new BoxDistributionDto(
            boxCounts.GetValueOrDefault(1),
            boxCounts.GetValueOrDefault(2),
            boxCounts.GetValueOrDefault(3),
            boxCounts.GetValueOrDefault(4),
            boxCounts.GetValueOrDefault(5));

        var sessions = await db.TrainingSessions
            .Where(s => s.UserId == userId && s.ListId == listId && s.CompletedAt != null)
            .OrderBy(s => s.StartedAt)
            .Select(s => new SessionHistoryEntryDto(
                s.Id,
                s.StartedAt,
                s.TotalQuestions > 0 ? Math.Round((double)s.CorrectAnswers / s.TotalQuestions * 100, 1) : 0))
            .ToListAsync();

        var vocabIds = db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id);

        var wrongCounts = await db.TrainingAnswers
            .Where(a => !a.IsCorrect && vocabIds.Contains(a.VocabularyId))
            .GroupBy(a => a.VocabularyId)
            .Select(g => new { VocabularyId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var wrongVocabIds = wrongCounts.Select(wc => wc.VocabularyId).ToList();

        var vocabs = await db.Vocabularies
            .Where(v => wrongVocabIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var boxEntryMap = await db.BoxEntries
            .Where(b => b.UserId == userId && wrongVocabIds.Contains(b.VocabularyId))
            .ToDictionaryAsync(b => b.VocabularyId, b => b.Box);

        var problemVocab = wrongCounts
            .Where(wc => vocabs.ContainsKey(wc.VocabularyId))
            .Select(wc =>
            {
                var vocab = vocabs[wc.VocabularyId];
                var box = boxEntryMap.GetValueOrDefault(wc.VocabularyId, 1);
                return new ProblemVocabularyDto(vocab.Term, wc.Count, box, vocab.Hint);
            })
            .ToList();

        return new ListProgressDto(listId, list.Name, boxDist, sessions.Count, sessions, problemVocab);
    }

    public async Task<ListProgressDto> GetGlobalProgressAsync(int userId)
    {
        var boxCounts = await db.BoxEntries
            .Where(b => b.UserId == userId)
            .GroupBy(b => b.Box)
            .Select(g => new { Box = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Box, x => x.Count);

        var boxDist = new BoxDistributionDto(
            boxCounts.GetValueOrDefault(1),
            boxCounts.GetValueOrDefault(2),
            boxCounts.GetValueOrDefault(3),
            boxCounts.GetValueOrDefault(4),
            boxCounts.GetValueOrDefault(5));

        var sessions = await db.TrainingSessions
            .Where(s => s.UserId == userId && s.CompletedAt != null)
            .OrderBy(s => s.StartedAt)
            .Select(s => new SessionHistoryEntryDto(
                s.Id,
                s.StartedAt,
                s.TotalQuestions > 0 ? Math.Round((double)s.CorrectAnswers / s.TotalQuestions * 100, 1) : 0))
            .ToListAsync();

        var userVocabIds = db.Vocabularies
            .Where(v => v.List.UserId == userId)
            .Select(v => v.Id);

        var wrongCounts = await db.TrainingAnswers
            .Where(a => !a.IsCorrect && userVocabIds.Contains(a.VocabularyId))
            .GroupBy(a => a.VocabularyId)
            .Select(g => new { VocabularyId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var wrongVocabIds = wrongCounts.Select(wc => wc.VocabularyId).ToList();

        var vocabs = await db.Vocabularies
            .Where(v => wrongVocabIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id);

        var boxEntryMap = await db.BoxEntries
            .Where(b => b.UserId == userId && wrongVocabIds.Contains(b.VocabularyId))
            .ToDictionaryAsync(b => b.VocabularyId, b => b.Box);

        var problemVocab = wrongCounts
            .Where(wc => vocabs.ContainsKey(wc.VocabularyId))
            .Select(wc =>
            {
                var vocab = vocabs[wc.VocabularyId];
                var box = boxEntryMap.GetValueOrDefault(wc.VocabularyId, 1);
                return new ProblemVocabularyDto(vocab.Term, wc.Count, box, vocab.Hint);
            })
            .ToList();

        return new ListProgressDto(0, "Gesamtfortschritt", boxDist, sessions.Count, sessions, problemVocab);
    }
}
