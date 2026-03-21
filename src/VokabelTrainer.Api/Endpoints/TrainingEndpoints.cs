using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class TrainingEndpoints
{
    public static WebApplication MapTrainingEndpoints(this WebApplication app)
    {
        app.MapPost("/form/training/start", async (HttpContext ctx, TrainingService trainingService) =>
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

        app.MapPost("/form/training/{sessionId:int}/submit", async (int sessionId, HttpContext ctx, TrainingService trainingService) =>
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

            if (int.TryParse(vocabIdStr, out var vocabId) && int.TryParse(directionStr, out var dirInt))
            {
                var direction = (Direction)dirInt;
                var feedback = await trainingService.SubmitAnswerAsync(sessionId, vocabId, direction, answer);

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
