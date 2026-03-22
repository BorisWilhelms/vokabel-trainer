// Services/VocabularyListService.cs
namespace VokabelTrainer.Api.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models.Lists;

public class VocabularyListService(AppDbContext db)
{
    public async Task<List<VocabularyListSummaryDto>> GetAllForUserAsync(int userId)
    {
        var lists = await db.VocabularyLists
            .Where(l => l.UserId == userId)
            .Select(l => new
            {
                l.Id, l.Name,
                l.SourceLanguageId,
                SourceLanguageName = l.SourceLanguage.DisplayName,
                SourceFlagSvg = l.SourceLanguage.FlagSvg,
                l.TargetLanguageId,
                TargetLanguageName = l.TargetLanguage.DisplayName,
                TargetFlagSvg = l.TargetLanguage.FlagSvg,
                VocabCount = l.Vocabularies.Count
            })
            .ToListAsync();

        var boxCounts = await db.BoxEntries
            .Where(b => b.UserId == userId)
            .GroupBy(b => new { b.Vocabulary.ListId, b.Box })
            .Select(g => new { g.Key.ListId, g.Key.Box, Count = g.Count() })
            .ToListAsync();

        return lists.Select(l =>
        {
            var listBoxes = boxCounts.Where(bc => bc.ListId == l.Id).ToList();
            BoxDistributionDto? dist = listBoxes.Count > 0
                ? new BoxDistributionDto(
                    listBoxes.Where(bc => bc.Box == 1).Select(bc => bc.Count).FirstOrDefault(),
                    listBoxes.Where(bc => bc.Box == 2).Select(bc => bc.Count).FirstOrDefault(),
                    listBoxes.Where(bc => bc.Box == 3).Select(bc => bc.Count).FirstOrDefault(),
                    listBoxes.Where(bc => bc.Box == 4).Select(bc => bc.Count).FirstOrDefault(),
                    listBoxes.Where(bc => bc.Box == 5).Select(bc => bc.Count).FirstOrDefault())
                : null;

            return new VocabularyListSummaryDto(
                l.Id, l.Name,
                l.SourceLanguageId, l.SourceLanguageName, l.SourceFlagSvg,
                l.TargetLanguageId, l.TargetLanguageName, l.TargetFlagSvg,
                l.VocabCount, dist);
        }).ToList();
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
            .AsTracking()
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
