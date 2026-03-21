namespace VokabelTrainer.Shared.Dtos.Lists;

public record CreateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);
