namespace VokabelTrainer.Shared.Dtos.Lists;

public record UpdateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);
