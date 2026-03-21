namespace VokabelTrainer.Api.Models.Languages;

public record UpdateLanguageRequest(string Code, string DisplayName, string? FlagSvg);
