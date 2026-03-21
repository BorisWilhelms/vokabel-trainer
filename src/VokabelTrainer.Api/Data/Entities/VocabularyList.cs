namespace VokabelTrainer.Api.Data.Entities;

public class VocabularyList
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Name { get; set; }
    public int SourceLanguageId { get; set; }
    public Language SourceLanguage { get; set; } = null!;
    public int TargetLanguageId { get; set; }
    public Language TargetLanguage { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<Vocabulary> Vocabularies { get; set; } = [];
}
