namespace VokabelTrainer.Api.Models.Lists;

public record UpdateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);
