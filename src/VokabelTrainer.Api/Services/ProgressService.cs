// Services/ProgressService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Shared.Dtos.Lists;
using VokabelTrainer.Shared.Dtos.Progress;

public class ProgressService(AppDbContext db)
{
    public async Task<ListProgressDto?> GetListProgressAsync(int userId, int listId)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == listId && l.UserId == userId)
            .FirstOrDefaultAsync();

        if (list is null) return null;

        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var boxEntries = await db.BoxEntries
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .ToListAsync();

        var boxDist = new BoxDistributionDto(
            boxEntries.Count(b => b.Box == 1),
            boxEntries.Count(b => b.Box == 2),
            boxEntries.Count(b => b.Box == 3),
            boxEntries.Count(b => b.Box == 4),
            boxEntries.Count(b => b.Box == 5));

        var sessions = await db.TrainingSessions
            .Where(s => s.UserId == userId && s.ListId == listId && s.CompletedAt != null)
            .OrderBy(s => s.StartedAt)
            .Select(s => new SessionHistoryEntryDto(
                s.Id,
                s.StartedAt,
                s.TotalQuestions > 0 ? Math.Round((double)s.CorrectAnswers / s.TotalQuestions * 100, 1) : 0))
            .ToListAsync();

        var wrongCounts = await db.TrainingAnswers
            .Where(a => !a.IsCorrect && vocabIds.Contains(a.VocabularyId))
            .GroupBy(a => a.VocabularyId)
            .Select(g => new { VocabularyId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var problemVocab = new List<ProblemVocabularyDto>();
        foreach (var wc in wrongCounts)
        {
            var vocab = await db.Vocabularies.FindAsync(wc.VocabularyId);
            var box = boxEntries.FirstOrDefault(b => b.VocabularyId == wc.VocabularyId)?.Box ?? 1;
            if (vocab is not null)
                problemVocab.Add(new ProblemVocabularyDto(vocab.Term, wc.Count, box));
        }

        return new ListProgressDto(listId, list.Name, boxDist, sessions.Count, sessions, problemVocab);
    }
}
