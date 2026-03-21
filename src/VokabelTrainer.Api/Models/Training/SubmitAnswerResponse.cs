namespace VokabelTrainer.Api.Models.Training;

public record SubmitAnswerResponse(bool IsCorrect, List<string> CorrectAnswers, int NewBox, bool SessionComplete);
