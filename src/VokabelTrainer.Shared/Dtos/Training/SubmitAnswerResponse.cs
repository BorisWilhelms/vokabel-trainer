namespace VokabelTrainer.Shared.Dtos.Training;

public record SubmitAnswerResponse(bool IsCorrect, List<string> CorrectAnswers, int NewBox, bool SessionComplete);
