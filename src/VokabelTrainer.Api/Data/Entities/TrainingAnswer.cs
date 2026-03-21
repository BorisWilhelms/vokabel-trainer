namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;

public class TrainingAnswer
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public TrainingSession Session { get; set; } = null!;
    public int VocabularyId { get; set; }
    public Vocabulary Vocabulary { get; set; } = null!;
    public Direction Direction { get; set; }
    public required string GivenAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
}
