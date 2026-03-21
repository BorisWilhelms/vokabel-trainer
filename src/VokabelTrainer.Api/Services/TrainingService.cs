namespace VokabelTrainer.Api.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models.Training;
using VokabelTrainer.Api.Models;

public class TrainingService(AppDbContext db, LeitnerService leitner)
{
    private static readonly Random Rng = new();

    public async Task<int> StartSessionAsync(int userId, int? listId, TrainingMode mode, int? maxVocabulary)
    {
        // Ensure box entries exist
        if (listId.HasValue)
        {
            await leitner.EnsureBoxEntriesAsync(userId, listId.Value);
        }
        else
        {
            var listIds = await db.VocabularyLists
                .Where(l => l.UserId == userId)
                .Select(l => l.Id)
                .ToListAsync();
            foreach (var lid in listIds)
                await leitner.EnsureBoxEntriesAsync(userId, lid);
        }

        var session = new TrainingSession
        {
            UserId = userId,
            ListId = listId,
            Mode = mode,
            MaxVocabulary = maxVocabulary,
            StartedAt = DateTime.UtcNow,
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    public async Task<TrainingQuestionDto?> GetNextQuestionAsync(int sessionId)
    {
        var session = await db.TrainingSessions
            .Include(s => s.List).ThenInclude(l => l!.SourceLanguage)
            .Include(s => s.List).ThenInclude(l => l!.TargetLanguage)
            .FirstAsync(s => s.Id == sessionId);

        var answeredVocabIds = await db.TrainingAnswers
            .Where(a => a.SessionId == sessionId)
            .Select(a => a.VocabularyId)
            .ToListAsync();

        // In Endlos mode, only exclude vocab that was answered correctly
        var correctlyAnsweredIds = session.Mode == TrainingMode.Endlos
            ? await db.TrainingAnswers
                .Where(a => a.SessionId == sessionId && a.IsCorrect)
                .Select(a => a.VocabularyId)
                .Distinct()
                .ToListAsync()
            : answeredVocabIds.Distinct().ToList();

        // Check MaxVocabulary limit: if we've already asked enough distinct vocab, stop
        // (In Endlos mode, re-asking wrong vocab doesn't count as "new")
        var distinctAskedCount = answeredVocabIds.Distinct().Count();
        if (session.MaxVocabulary.HasValue && distinctAskedCount >= session.MaxVocabulary.Value
            && (session.Mode != TrainingMode.Endlos || correctlyAnsweredIds.Count >= session.MaxVocabulary.Value))
        {
            return null;
        }

        // Get due vocabulary, excluding already completed
        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIdsInList = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value)
                .Select(v => v.Id);
            query = query.Where(b => vocabIdsInList.Contains(b.VocabularyId));
        }

        var dueEntries = await query
            .Where(b => !correctlyAnsweredIds.Contains(b.VocabularyId))
            .OrderBy(b => b.Box)
            .ToListAsync();

        // Shuffle within same box level (client-side, can't do Random in SQL)
        dueEntries = dueEntries
            .GroupBy(b => b.Box)
            .SelectMany(g => g.OrderBy(_ => Rng.Next()))
            .ToList();

        // Limit to MaxVocabulary: only consider new vocab up to the limit
        if (session.MaxVocabulary.HasValue)
        {
            var remaining = session.MaxVocabulary.Value - distinctAskedCount;
            if (remaining > 0 && session.Mode != TrainingMode.Endlos)
            {
                dueEntries = dueEntries.Take(remaining).ToList();
            }
        }

        // In Endlos mode, also avoid recently wrong-answered vocab (delay re-asking)
        if (session.Mode == TrainingMode.Endlos && dueEntries.Count > 1)
        {
            var recentWrongIds = await db.TrainingAnswers
                .Where(a => a.SessionId == sessionId && !a.IsCorrect)
                .OrderByDescending(a => a.AnsweredAt)
                .Take(3)
                .Select(a => a.VocabularyId)
                .ToListAsync();

            var delayed = dueEntries.Where(e => !recentWrongIds.Contains(e.VocabularyId)).ToList();
            if (delayed.Count > 0)
                dueEntries = delayed;
        }

        var nextEntry = dueEntries.FirstOrDefault();
        if (nextEntry is null)
            return null;

        var vocab = await db.Vocabularies
            .Include(v => v.List).ThenInclude(l => l.SourceLanguage)
            .Include(v => v.List).ThenInclude(l => l.TargetLanguage)
            .FirstAsync(v => v.Id == nextEntry.VocabularyId);

        var direction = Rng.Next(2) == 0 ? Direction.SourceToTarget : Direction.TargetToSource;
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var prompt = direction == Direction.SourceToTarget ? vocab.Term : translations[Rng.Next(translations.Count)];

        var totalDue = session.Mode == TrainingMode.Endlos
            ? dueEntries.Count + correctlyAnsweredIds.Count
            : await CountTotalDueAsync(session);
        var totalCount = session.MaxVocabulary.HasValue
            ? Math.Min(session.MaxVocabulary.Value, totalDue)
            : totalDue;
        var currentIndex = distinctAskedCount + 1;

        return new TrainingQuestionDto(
            session.Id, vocab.Id, prompt, direction,
            vocab.List.SourceLanguage.DisplayName, vocab.List.SourceLanguage.FlagSvg,
            vocab.List.TargetLanguage.DisplayName, vocab.List.TargetLanguage.FlagSvg,
            currentIndex, totalCount);
    }

    public async Task<SubmitAnswerResponse> SubmitAnswerAsync(
        int sessionId, int vocabularyId, Direction direction, string answer, double? responseSeconds = null)
    {
        var session = await db.TrainingSessions.FirstAsync(s => s.Id == sessionId);
        var vocab = await db.Vocabularies.FirstAsync(v => v.Id == vocabularyId);
        var boxEntry = await db.BoxEntries
            .FirstAsync(b => b.UserId == session.UserId && b.VocabularyId == vocabularyId);

        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var trimmedAnswer = answer.Trim();

        // Direction-aware answer checking:
        // SourceToTarget: prompted with Term, answer must match a Translation
        // TargetToSource: prompted with a Translation, answer must match Term
        var isCorrect = direction == Direction.SourceToTarget
            ? translations.Any(t => string.Equals(t.Trim(), trimmedAnswer, StringComparison.OrdinalIgnoreCase))
            : string.Equals(vocab.Term.Trim(), trimmedAnswer, StringComparison.OrdinalIgnoreCase);

        var trainingAnswer = new TrainingAnswer
        {
            SessionId = sessionId,
            VocabularyId = vocabularyId,
            Direction = direction,
            GivenAnswer = answer,
            IsCorrect = isCorrect,
            ResponseSeconds = responseSeconds,
            AnsweredAt = DateTime.UtcNow
        };
        db.TrainingAnswers.Add(trainingAnswer);

        if (isCorrect)
        {
            LeitnerService.Promote(boxEntry);
            session.CorrectAnswers++;
        }
        else
        {
            LeitnerService.Demote(boxEntry);
        }
        session.TotalQuestions++;

        // Check if session is complete (no more due vocabulary)
        var sessionComplete = !await HasRemainingQuestionsAsync(session);

        if (sessionComplete)
        {
            session.CompletedAt = DateTime.UtcNow;
            // Decrement counters for affected lists
            if (session.ListId.HasValue)
                await leitner.DecrementSessionCountersAsync(session.UserId, session.ListId.Value);
            else
            {
                var listIds = await db.VocabularyLists
                    .Where(l => l.UserId == session.UserId)
                    .Select(l => l.Id).ToListAsync();
                foreach (var lid in listIds)
                    await leitner.DecrementSessionCountersAsync(session.UserId, lid);
            }
        }

        await db.SaveChangesAsync();

        var correctAnswers = direction == Direction.SourceToTarget
            ? translations : [vocab.Term];

        return new SubmitAnswerResponse(isCorrect, correctAnswers, boxEntry.Box, sessionComplete);
    }

    private async Task<bool> HasRemainingQuestionsAsync(TrainingSession session)
    {
        var allAnsweredIds = await db.TrainingAnswers
            .Where(a => a.SessionId == session.Id)
            .Select(a => a.VocabularyId).Distinct().ToListAsync();

        var correctlyAnsweredIds = session.Mode == TrainingMode.Endlos
            ? await db.TrainingAnswers
                .Where(a => a.SessionId == session.Id && a.IsCorrect)
                .Select(a => a.VocabularyId).Distinct().ToListAsync()
            : allAnsweredIds;

        // MaxVocabulary limit reached
        if (session.MaxVocabulary.HasValue)
        {
            if (session.Mode == TrainingMode.Endlos)
            {
                if (correctlyAnsweredIds.Count >= session.MaxVocabulary.Value)
                    return false;
            }
            else
            {
                if (allAnsweredIds.Count >= session.MaxVocabulary.Value)
                    return false;
            }
        }

        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIds = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value).Select(v => v.Id);
            query = query.Where(b => vocabIds.Contains(b.VocabularyId));
        }

        return await query.AnyAsync(b => !correctlyAnsweredIds.Contains(b.VocabularyId));
    }

    public async Task<SessionResultDto?> GetSessionResultAsync(int sessionId)
    {
        var session = await db.TrainingSessions
            .Include(s => s.Answers).ThenInclude(a => a.Vocabulary)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return null;

        var wrongAnswers = session.Answers
            .Where(a => !a.IsCorrect)
            .GroupBy(a => a.VocabularyId)
            .Select(g =>
            {
                var vocab = g.First().Vocabulary;
                var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
                var last = g.Last();
                return new WrongAnswerDto(vocab.Term, translations, last.GivenAnswer, last.ResponseSeconds, vocab.Hint);
            })
            .ToList();

        var successRate = session.TotalQuestions > 0
            ? (double)session.CorrectAnswers / session.TotalQuestions * 100
            : 0;

        // Average response time, excluding AFK answers (> 60s)
        var validTimes = session.Answers
            .Where(a => a.ResponseSeconds.HasValue && a.ResponseSeconds.Value <= 60)
            .Select(a => a.ResponseSeconds!.Value)
            .ToList();
        var avgSeconds = validTimes.Count > 0 ? Math.Round(validTimes.Average(), 1) : (double?)null;

        return new SessionResultDto(
            session.Id, session.TotalQuestions, session.CorrectAnswers,
            Math.Round(successRate, 1), avgSeconds, wrongAnswers);
    }

    public async Task<int?> GetSessionListIdAsync(int sessionId)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        return session?.ListId;
    }

    public async Task CompleteSessionIfNeededAsync(int sessionId)
    {
        var session = await db.TrainingSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null || session.CompletedAt.HasValue) return;

        session.CompletedAt = DateTime.UtcNow;
        if (session.ListId.HasValue)
            await leitner.DecrementSessionCountersAsync(session.UserId, session.ListId.Value);
        else
        {
            var listIds = await db.VocabularyLists
                .Where(l => l.UserId == session.UserId)
                .Select(l => l.Id).ToListAsync();
            foreach (var lid in listIds)
                await leitner.DecrementSessionCountersAsync(session.UserId, lid);
        }
        await db.SaveChangesAsync();
    }

    public async Task AbortSessionAsync(int sessionId)
    {
        var session = await db.TrainingSessions.FirstAsync(s => s.Id == sessionId);
        session.CompletedAt = DateTime.UtcNow;

        if (session.ListId.HasValue)
            await leitner.DecrementSessionCountersAsync(session.UserId, session.ListId.Value);
        else
        {
            var listIds = await db.VocabularyLists
                .Where(l => l.UserId == session.UserId)
                .Select(l => l.Id).ToListAsync();
            foreach (var lid in listIds)
                await leitner.DecrementSessionCountersAsync(session.UserId, lid);
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountTotalDueAsync(TrainingSession session)
    {
        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIds = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value)
                .Select(v => v.Id);
            query = query.Where(b => vocabIds.Contains(b.VocabularyId));
        }

        return await query.CountAsync();
    }
}
