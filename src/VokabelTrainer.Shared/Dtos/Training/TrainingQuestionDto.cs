using VokabelTrainer.Shared.Models;

namespace VokabelTrainer.Shared.Dtos.Training;

public record TrainingQuestionDto(
    int SessionId,
    int VocabularyId,
    string Prompt,
    Direction Direction,
    string SourceLanguageName,
    string? SourceFlagSvg,
    string TargetLanguageName,
    string? TargetFlagSvg,
    int CurrentIndex,
    int TotalCount);
