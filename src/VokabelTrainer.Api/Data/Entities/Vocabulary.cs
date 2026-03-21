namespace VokabelTrainer.Api.Data.Entities;

public class Vocabulary
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public VocabularyList List { get; set; } = null!;
    public required string Term { get; set; }
    public required string Translations { get; set; } // JSON array
    public string? Hint { get; set; }
    public List<BoxEntry> BoxEntries { get; set; } = [];
    public List<TrainingAnswer> TrainingAnswers { get; set; } = [];
}
