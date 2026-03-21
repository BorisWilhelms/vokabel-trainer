namespace VokabelTrainer.Api.Models.Languages;

public record CreateLanguageRequest(string Code, string DisplayName, string? FlagSvg);
