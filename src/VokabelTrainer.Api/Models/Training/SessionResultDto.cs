namespace VokabelTrainer.Api.Models.Training;

public record WrongAnswerDto(string Term, List<string> CorrectTranslations, string GivenAnswer);

public record SessionResultDto(
    int SessionId,
    int TotalQuestions,
    int CorrectAnswers,
    double SuccessRate,
    List<WrongAnswerDto> WrongAnswers);
