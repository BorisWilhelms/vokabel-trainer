using VokabelTrainer.Shared.Models;

namespace VokabelTrainer.Shared.Dtos.Training;

public record StartSessionRequest(int? ListId, TrainingMode Mode, int? MaxVocabulary);
