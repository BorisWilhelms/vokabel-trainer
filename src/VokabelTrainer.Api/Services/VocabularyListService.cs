// Services/VocabularyListService.cs
namespace VokabelTrainer.Api.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Lists;

public class VocabularyListService(AppDbContext db)
{
    public async Task<List<VocabularyListSummaryDto>> GetAllForUserAsync(int userId)
    {
        var lists = await db.VocabularyLists
            .Where(l => l.UserId == userId)
            .Include(l => l.SourceLanguage)
            .Include(l => l.TargetLanguage)
            .Include(l => l.Vocabularies)
            .ToListAsync();

        var result = new List<VocabularyListSummaryDto>();
        foreach (var list in lists)
        {
            var vocabIds = list.Vocabularies.Select(v => v.Id).ToList();
            var boxEntries = await db.BoxEntries
                .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
                .ToListAsync();

            BoxDistributionDto? dist = boxEntries.Count > 0
                ? new BoxDistributionDto(
                    boxEntries.Count(b => b.Box == 1),
                    boxEntries.Count(b => b.Box == 2),
                    boxEntries.Count(b => b.Box == 3),
                    boxEntries.Count(b => b.Box == 4),
                    boxEntries.Count(b => b.Box == 5))
                : null;

            result.Add(new VocabularyListSummaryDto(
                list.Id, list.Name,
                list.SourceLanguageId, list.SourceLanguage.DisplayName, list.SourceLanguage.FlagSvg,
                list.TargetLanguageId, list.TargetLanguage.DisplayName, list.TargetLanguage.FlagSvg,
                list.Vocabularies.Count, dist));
        }

        return result;
    }

    public async Task<VocabularyListDto?> GetByIdAsync(int id, int userId)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == id && l.UserId == userId)
            .Include(l => l.SourceLanguage)
            .Include(l => l.TargetLanguage)
            .Include(l => l.Vocabularies)
            .FirstOrDefaultAsync();

        if (list is null) return null;

        var entries = list.Vocabularies.Select(v =>
            new VocabularyEntryDto(v.Id, v.Term,
                JsonSerializer.Deserialize<List<string>>(v.Translations)!))
            .ToList();

        return new VocabularyListDto(list.Id, list.Name,
            list.SourceLanguageId, list.SourceLanguage.DisplayName,
            list.TargetLanguageId, list.TargetLanguage.DisplayName,
            entries);
    }

    public async Task<int> CreateAsync(int userId, CreateVocabularyListRequest request)
    {
        var parsed = VocabularyParser.Parse(request.RawVocabulary);
        var list = new VocabularyList
        {
            UserId = userId,
            Name = request.Name,
            SourceLanguageId = request.SourceLanguageId,
            TargetLanguageId = request.TargetLanguageId,
            CreatedAt = DateTime.UtcNow,
            Vocabularies = parsed.Select(p => new Vocabulary
            {
                Term = p.Term,
                Translations = JsonSerializer.Serialize(p.Translations)
            }).ToList()
        };
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();
        return list.Id;
    }

    public async Task<bool> UpdateAsync(int id, int userId, UpdateVocabularyListRequest request)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == id && l.UserId == userId)
            .Include(l => l.Vocabularies)
            .FirstOrDefaultAsync();

        if (list is null) return false;

        list.Name = request.Name;
        list.SourceLanguageId = request.SourceLanguageId;
        list.TargetLanguageId = request.TargetLanguageId;

        // Replace all vocabulary (cascade deletes BoxEntry and TrainingAnswer)
        db.Vocabularies.RemoveRange(list.Vocabularies);

        var parsed = VocabularyParser.Parse(request.RawVocabulary);
        list.Vocabularies = parsed.Select(p => new Vocabulary
        {
            Term = p.Term,
            Translations = JsonSerializer.Serialize(p.Translations)
        }).ToList();

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == id && l.UserId == userId)
            .FirstOrDefaultAsync();

        if (list is null) return false;
        db.VocabularyLists.Remove(list);
        await db.SaveChangesAsync();
        return true;
    }
}
