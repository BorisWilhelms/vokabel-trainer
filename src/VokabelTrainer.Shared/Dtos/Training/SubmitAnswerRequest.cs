using VokabelTrainer.Shared.Models;

namespace VokabelTrainer.Shared.Dtos.Training;

public record SubmitAnswerRequest(int SessionId, int VocabularyId, Direction Direction, string Answer);
