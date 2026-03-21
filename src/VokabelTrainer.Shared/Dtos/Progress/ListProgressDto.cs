using VokabelTrainer.Shared.Dtos.Lists;

namespace VokabelTrainer.Shared.Dtos.Progress;

public record ListProgressDto(
    int ListId,
    string ListName,
    BoxDistributionDto BoxDistribution,
    int TotalSessions,
    List<SessionHistoryEntryDto> SessionHistory,
    List<ProblemVocabularyDto> ProblemVocabulary);
