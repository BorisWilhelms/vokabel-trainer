namespace VokabelTrainer.Api.Models.Lists;

public record CreateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);
