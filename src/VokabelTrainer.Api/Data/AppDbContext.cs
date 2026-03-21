namespace VokabelTrainer.Api.Data;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data.Entities;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<VocabularyList> VocabularyLists => Set<VocabularyList>();
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<BoxEntry> BoxEntries => Set<BoxEntry>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingAnswer> TrainingAnswers => Set<TrainingAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<Language>()
            .HasIndex(l => l.Code).IsUnique();

        modelBuilder.Entity<BoxEntry>()
            .HasIndex(b => new { b.UserId, b.VocabularyId }).IsUnique();

        modelBuilder.Entity<VocabularyList>()
            .HasOne(vl => vl.SourceLanguage)
            .WithMany()
            .HasForeignKey(vl => vl.SourceLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VocabularyList>()
            .HasOne(vl => vl.TargetLanguage)
            .WithMany()
            .HasForeignKey(vl => vl.TargetLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Vocabulary>()
            .HasMany(v => v.BoxEntries)
            .WithOne(b => b.Vocabulary)
            .HasForeignKey(b => b.VocabularyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vocabulary>()
            .HasMany(v => v.TrainingAnswers)
            .WithOne(a => a.Vocabulary)
            .HasForeignKey(a => a.VocabularyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
