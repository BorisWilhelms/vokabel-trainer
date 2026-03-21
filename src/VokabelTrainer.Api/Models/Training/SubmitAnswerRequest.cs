namespace VokabelTrainer.Api.Models.Training;

public record SubmitAnswerRequest(int SessionId, int VocabularyId, Direction Direction, string Answer);
