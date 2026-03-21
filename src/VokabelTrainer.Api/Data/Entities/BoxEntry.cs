namespace VokabelTrainer.Api.Data.Entities;

public class BoxEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int VocabularyId { get; set; }
    public Vocabulary Vocabulary { get; set; } = null!;
    public int Box { get; set; } = 1;
    public int SessionsUntilReview { get; set; }
}
