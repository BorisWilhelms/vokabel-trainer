namespace VokabelTrainer.Api.Models.Training;

public record StartSessionRequest(int? ListId, TrainingMode Mode, int? MaxVocabulary);
