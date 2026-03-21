namespace VokabelTrainer.Shared.Dtos.Lists;

public record BoxDistributionDto(int Box1, int Box2, int Box3, int Box4, int Box5);

public record VocabularyListSummaryDto(
    int Id,
    string Name,
    int SourceLanguageId,
    string SourceLanguageName,
    string? SourceFlagSvg,
    int TargetLanguageId,
    string TargetLanguageName,
    string? TargetFlagSvg,
    int VocabularyCount,
    BoxDistributionDto? BoxDistribution);
