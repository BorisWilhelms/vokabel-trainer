namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsInitialized { get; set; }
    public UserRole Role { get; set; }
    public List<VocabularyList> VocabularyLists { get; set; } = [];
    public List<BoxEntry> BoxEntries { get; set; } = [];
    public List<TrainingSession> TrainingSessions { get; set; } = [];
}
