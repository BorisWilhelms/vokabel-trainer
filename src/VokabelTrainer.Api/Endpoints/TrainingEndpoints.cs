using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages;
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

        app.MapGet("/training/{sessionId:int}", async (int sessionId, TrainingService trainingService, HttpContext ctx,
            string? mode, string? fc, string? fa, string? fp, string? fg) =>
        {
            var question = await trainingService.GetNextQuestionAsync(sessionId);
            if (question is null)
            {
                await trainingService.CompleteSessionIfNeededAsync(sessionId);
                return Results.Redirect($"/training/result/{sessionId}");
            }

            bool? feedbackCorrect = fc is not null ? fc == "1" : null;
            var isEndlos = string.Equals(mode, "Endlos", StringComparison.OrdinalIgnoreCase);

            return new RazorComponentResult<Training>(new
            {
                SessionId = sessionId,
                Question = question,
                FeedbackCorrect = feedbackCorrect,
                FeedbackAnswers = fa,
                FeedbackPrompt = fp,
                FeedbackGiven = fg,
                Mode = mode,
                IsEndlos = isEndlos,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapGet("/training/result/{sessionId:int}", async (int sessionId, TrainingService trainingService, ProgressService progressService, HttpContext ctx) =>
        {
            // Ensure session is completed (handles cases where user navigated away)
            await trainingService.CompleteSessionIfNeededAsync(sessionId);
            var result = await trainingService.GetSessionResultAsync(sessionId);
            var userId = ctx.GetUserId();

            // Load progress for the list (or global if cross-list training)
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

        app.MapPost("/training/{sessionId:int}/submit", async (int sessionId, HttpContext ctx, TrainingService trainingService) =>
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
            double? responseSeconds = double.TryParse(responseSecondsStr, System.Globalization.CultureInfo.InvariantCulture, out var rs) ? rs : null;

            if (int.TryParse(vocabIdStr, out var vocabId) && int.TryParse(directionStr, out var dirInt))
            {
                var direction = (Direction)dirInt;
                var feedback = await trainingService.SubmitAnswerAsync(sessionId, vocabId, direction, answer, responseSeconds);

                if (feedback.SessionComplete)
                {
                    return Results.Redirect($"/training/result/{sessionId}");
                }

                var correct = feedback.IsCorrect ? "1" : "0";
                var correctAnswers = Uri.EscapeDataString(string.Join(", ", feedback.CorrectAnswers));
                var prompt = Uri.EscapeDataString(form["PreviousPrompt"].FirstOrDefault() ?? "");
                var givenAnswer = Uri.EscapeDataString(answer);

                return Results.Redirect(
                    $"/training/{sessionId}?mode={mode}&fc={correct}&fa={correctAnswers}&fp={prompt}&fg={givenAnswer}");
            }

            return Results.Redirect($"/training/{sessionId}?mode={mode}");
        }).RequireAuthorization().DisableAntiforgery();

        return app;
    }
}
