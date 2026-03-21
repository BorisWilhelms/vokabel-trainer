namespace VokabelTrainer.Shared.Dtos.Languages;

public record CreateLanguageRequest(string Code, string DisplayName, string? FlagSvg);
