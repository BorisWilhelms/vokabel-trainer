namespace VokabelTrainer.Api.Models.Lists;

public record VocabularyEntryDto(int Id, string Term, List<string> Translations);

public record VocabularyListDto(
    int Id,
    string Name,
    int SourceLanguageId,
    string SourceLanguageName,
    int TargetLanguageId,
    string TargetLanguageName,
    List<VocabularyEntryDto> Entries);
