using VokabelTrainer.Api.Models.Lists;

namespace VokabelTrainer.Api.Models.Progress;

public record ListProgressDto(
    int ListId,
    string ListName,
    BoxDistributionDto BoxDistribution,
    int TotalSessions,
    List<SessionHistoryEntryDto> SessionHistory,
    List<ProblemVocabularyDto> ProblemVocabulary);
