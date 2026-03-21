// Services/LanguageService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Languages;

public class LanguageService(AppDbContext db)
{
    public async Task<List<LanguageDto>> GetAllAsync()
        => await db.Languages
            .Select(l => new LanguageDto(l.Id, l.Code, l.DisplayName, l.FlagSvg))
            .ToListAsync();

    public async Task<LanguageDto> CreateAsync(CreateLanguageRequest request)
    {
        var language = new Language
        {
            Code = request.Code,
            DisplayName = request.DisplayName,
            FlagSvg = request.FlagSvg
        };
        db.Languages.Add(language);
        await db.SaveChangesAsync();
        return new LanguageDto(language.Id, language.Code, language.DisplayName, language.FlagSvg);
    }

    public async Task<LanguageDto?> UpdateAsync(int id, UpdateLanguageRequest request)
    {
        var language = await db.Languages.FindAsync(id);
        if (language is null) return null;
        language.Code = request.Code;
        language.DisplayName = request.DisplayName;
        language.FlagSvg = request.FlagSvg;
        await db.SaveChangesAsync();
        return new LanguageDto(language.Id, language.Code, language.DisplayName, language.FlagSvg);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var language = await db.Languages.FindAsync(id);
        if (language is null) return false;

        var isInUse = await db.VocabularyLists
            .AnyAsync(l => l.SourceLanguageId == id || l.TargetLanguageId == id);
        if (isInUse) return false; // Cannot delete language in use

        db.Languages.Remove(language);
        await db.SaveChangesAsync();
        return true;
    }
}
