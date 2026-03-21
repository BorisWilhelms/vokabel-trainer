namespace VokabelTrainer.Api.Models.Progress;

public record ProblemVocabularyDto(string Term, int TimesWrong, int CurrentBox);
