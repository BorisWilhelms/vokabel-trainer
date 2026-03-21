namespace VokabelTrainer.Api.Models.Training;

public record WrongAnswerDto(string Term, List<string> CorrectTranslations, string GivenAnswer, double? ResponseSeconds, string? Hint);

public record SessionResultDto(
    int SessionId,
    int TotalQuestions,
    int CorrectAnswers,
    double SuccessRate,
    double? AverageResponseSeconds,
    List<WrongAnswerDto> WrongAnswers);
