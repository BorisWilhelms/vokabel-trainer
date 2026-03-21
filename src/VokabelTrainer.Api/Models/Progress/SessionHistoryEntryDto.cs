namespace VokabelTrainer.Api.Models.Progress;

public record SessionHistoryEntryDto(int SessionId, DateTime Date, double SuccessRate);
