using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Models.Lists;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class TrainingEndpoints
{
    public static WebApplication MapTrainingEndpoints(this WebApplication app)
    {
        app.MapGet("/training/start", async (VocabularyListService listService, HttpContext ctx) =>
        {
            return new RazorComponentResult<TrainingStart>(new
            {
                ListId = (int?)null,
                ListSummary = (VocabularyListSummaryDto?)null,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapGet("/training/start/{listId:int}", async (int listId, VocabularyListService listService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            var allLists = await listService.GetAllForUserAsync(userId);
            var listSummary = allLists.FirstOrDefault(l => l.Id == listId);

            return new RazorComponentResult<TrainingStart>(new
            {
                ListId = (int?)listId,
                ListSummary = listSummary,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapGet("/training/{sessionId:int}", async (int sessionId, TrainingService trainingService, HttpContext ctx, string? mode) =>
        {
            var question = await trainingService.GetNextQuestionAsync(sessionId);
            if (question is null)
            {
                await trainingService.CompleteSessionIfNeededAsync(sessionId);
                return Results.Redirect($"/training/result/{sessionId}");
            }

            var isEndlos = string.Equals(mode, "Endlos", StringComparison.OrdinalIgnoreCase);

            return new RazorComponentResult<Training>(new
            {
                SessionId = sessionId,
                Question = question,
                Feedback = (AnswerFeedback?)null,
                Mode = mode,
                IsEndlos = isEndlos,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapGet("/training/result/{sessionId:int}", async (int sessionId, TrainingService trainingService, ProgressService progressService, HttpContext ctx) =>
        {
            await trainingService.CompleteSessionIfNeededAsync(sessionId);
            var result = await trainingService.GetSessionResultAsync(sessionId);
            var userId = ctx.GetUserId();

            var listId = await trainingService.GetSessionListIdAsync(sessionId);
            var progress = listId.HasValue
                ? await progressService.GetListProgressAsync(userId, listId.Value)
                : await progressService.GetGlobalProgressAsync(userId);

            return new RazorComponentResult<SessionResult>(new
            {
                Result = result,
                Progress = progress,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapPost("/training/start", async (HttpContext ctx, TrainingService trainingService) =>
        {
            var userId = ctx.GetUserId();
            var form = await ctx.Request.ReadFormAsync();

            var selectedMode = form["SelectedMode"].FirstOrDefault() ?? "SinglePass";
            var listIdStr = form["ListId"].FirstOrDefault();
            var maxVocabStr = form["MaxVocabularyInput"].FirstOrDefault();

            int? listId = int.TryParse(listIdStr, out var lid) ? lid : null;
            int? maxVocab = int.TryParse(maxVocabStr, out var mv) ? mv : null;

            var mode = selectedMode == "Endlos" ? TrainingMode.Endlos : TrainingMode.SinglePass;
            var sessionId = await trainingService.StartSessionAsync(userId, listId, mode, maxVocab);

            return Results.Redirect($"/training/{sessionId}?mode={mode}");
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/training/{sessionId:int}/submit", async (int sessionId, HttpContext ctx,
            TrainingService trainingService, AiService aiService, AppDbContext db) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var action = form["Action"].FirstOrDefault();
            var mode = form["Mode"].FirstOrDefault() ?? "";

            if (string.Equals(action, "abort", StringComparison.OrdinalIgnoreCase))
            {
                await trainingService.AbortSessionAsync(sessionId);
                return Results.Redirect($"/training/result/{sessionId}");
            }

            var answer = form["Answer"].FirstOrDefault() ?? "";
            var vocabIdStr = form["VocabularyId"].FirstOrDefault();
            var directionStr = form["QuestionDirection"].FirstOrDefault();
            var responseSecondsStr = form["ResponseSeconds"].FirstOrDefault();
            double? responseSeconds = double.TryParse(responseSecondsStr,
                System.Globalization.CultureInfo.InvariantCulture, out var rs) ? rs : null;

            if (!int.TryParse(vocabIdStr, out var vocabId) || !int.TryParse(directionStr, out var dirInt))
            {
                return Results.Redirect($"/training/{sessionId}?mode={mode}");
            }

            var direction = (Direction)dirInt;
            var previousPrompt = form["PreviousPrompt"].FirstOrDefault() ?? "";
            var submitFeedback = await trainingService.SubmitAnswerAsync(sessionId, vocabId, direction, answer, responseSeconds);

            // Generate hint for wrong answers
            string? hint = null;
            if (!submitFeedback.IsCorrect && aiService.IsConfigured)
            {
                var vocab = await db.Vocabularies
                    .Include(v => v.List).ThenInclude(l => l.SourceLanguage)
                    .Include(v => v.List).ThenInclude(l => l.TargetLanguage)
                    .FirstOrDefaultAsync(v => v.Id == vocabId);
                if (vocab is not null)
                {
                    if (vocab.Hint is null)
                    {
                        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
                        vocab.Hint = await aiService.GenerateHintAsync(
                            vocab.Term, translations,
                            vocab.List.SourceLanguage.DisplayName,
                            vocab.List.TargetLanguage.DisplayName);
                        await db.SaveChangesAsync();
                    }
                    hint = vocab.Hint;
                }
            }

            if (submitFeedback.SessionComplete)
            {
                return Results.Redirect($"/training/result/{sessionId}");
            }

            // Load next question and render directly
            var question = await trainingService.GetNextQuestionAsync(sessionId);
            if (question is null)
            {
                await trainingService.CompleteSessionIfNeededAsync(sessionId);
                return Results.Redirect($"/training/result/{sessionId}");
            }

            var isEndlos = string.Equals(mode, "Endlos", StringComparison.OrdinalIgnoreCase);
            var feedback = new AnswerFeedback(
                submitFeedback.IsCorrect, previousPrompt,
                string.Join(", ", submitFeedback.CorrectAnswers),
                answer, hint, vocabId);

            return new RazorComponentResult<Training>(new
            {
                SessionId = sessionId,
                Question = question,
                Feedback = (AnswerFeedback?)feedback,
                Mode = mode,
                IsEndlos = isEndlos,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/training/{sessionId:int}/regenerate-hint/{vocabId:int}", async (
            int sessionId, int vocabId, HttpContext ctx,
            TrainingService trainingService, AiService aiService, AppDbContext db) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var mode = form["Mode"].FirstOrDefault() ?? "";

            var vocab = await db.Vocabularies
                .Include(v => v.List).ThenInclude(l => l.SourceLanguage)
                .Include(v => v.List).ThenInclude(l => l.TargetLanguage)
                .FirstOrDefaultAsync(v => v.Id == vocabId);

            string? hint = null;
            if (vocab is not null)
            {
                var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
                vocab.Hint = await aiService.GenerateHintAsync(
                    vocab.Term, translations,
                    vocab.List.SourceLanguage.DisplayName,
                    vocab.List.TargetLanguage.DisplayName);
                await db.SaveChangesAsync();
                hint = vocab.Hint;
            }

            // Re-render the current training page with updated hint
            var question = await trainingService.GetNextQuestionAsync(sessionId);
            if (question is null)
            {
                return Results.Redirect($"/training/result/{sessionId}");
            }

            var isEndlos = string.Equals(mode, "Endlos", StringComparison.OrdinalIgnoreCase);

            // Reconstruct feedback from form hidden fields
            var feedback = new AnswerFeedback(
                false,
                form["FeedbackPrompt"].FirstOrDefault() ?? "",
                form["FeedbackAnswers"].FirstOrDefault() ?? "",
                form["FeedbackGiven"].FirstOrDefault() ?? "",
                hint, vocabId);

            return new RazorComponentResult<Training>(new
            {
                SessionId = sessionId,
                Question = question,
                Feedback = (AnswerFeedback?)feedback,
                Mode = mode,
                IsEndlos = isEndlos,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization().DisableAntiforgery();

        return app;
    }
}

public record AnswerFeedback(bool IsCorrect, string Prompt, string CorrectAnswers, string GivenAnswer, string? Hint, int VocabId);
