namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Models;

public class TrainingSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? ListId { get; set; }
    public VocabularyList? List { get; set; }
    public TrainingMode Mode { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public List<TrainingAnswer> Answers { get; set; } = [];
}
