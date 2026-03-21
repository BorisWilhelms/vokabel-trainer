namespace VokabelTrainer.Shared.Dtos.Languages;

public record UpdateLanguageRequest(string Code, string DisplayName, string? FlagSvg);
