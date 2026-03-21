namespace VokabelTrainer.Shared.Dtos.Progress;

public record SessionHistoryEntryDto(int SessionId, DateTime Date, double SuccessRate);
