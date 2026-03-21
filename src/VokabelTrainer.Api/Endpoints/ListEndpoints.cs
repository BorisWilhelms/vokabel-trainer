using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Models.Lists;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class ListEndpoints
{
    public static WebApplication MapListEndpoints(this WebApplication app)
    {
        app.MapGet("/lists/new", async (LanguageService languageService, HttpContext ctx) =>
        {
            var languages = await languageService.GetAllAsync();

            return new RazorComponentResult<ListEditor>(new
            {
                Id = (int?)null,
                Name = "",
                SourceLanguageId = 0,
                TargetLanguageId = 0,
                RawVocabulary = "",
                Languages = languages,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapGet("/lists/{id:int}/edit", async (int id, VocabularyListService listService, LanguageService languageService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            var list = await listService.GetByIdAsync(id, userId);
            if (list is null)
            {
                return Results.Redirect("/");
            }

            var languages = await languageService.GetAllAsync();
            var rawVocabulary = string.Join("\n",
                list.Entries.Select(e => $"{e.Term} = {string.Join(", ", e.Translations)}"));

            return new RazorComponentResult<ListEditor>(new
            {
                Id = (int?)id,
                Name = list.Name,
                SourceLanguageId = list.SourceLanguageId,
                TargetLanguageId = list.TargetLanguageId,
                RawVocabulary = rawVocabulary,
                Languages = languages,
                IsAdmin = ctx.User.IsInRole("Admin")
            });
        }).RequireAuthorization();

        app.MapPost("/lists", async (HttpContext ctx, VocabularyListService listService) =>
        {
            var userId = ctx.GetUserId();
            var form = await ctx.Request.ReadFormAsync();

            var name = form["Name"].FirstOrDefault() ?? "";
            var sourceLanguageId = int.TryParse(form["SourceLanguageId"].FirstOrDefault(), out var sid) ? sid : 0;
            var targetLanguageId = int.TryParse(form["TargetLanguageId"].FirstOrDefault(), out var tid) ? tid : 0;
            var rawVocabulary = form["RawVocabulary"].FirstOrDefault() ?? "";

            var request = new CreateVocabularyListRequest(name, sourceLanguageId, targetLanguageId, rawVocabulary);
            await listService.CreateAsync(userId, request);

            return Results.Redirect("/");
        }).RequireAuthorization().DisableAntiforgery();

        app.MapPost("/lists/{id:int}", async (int id, HttpContext ctx, VocabularyListService listService) =>
        {
            var userId = ctx.GetUserId();
            var form = await ctx.Request.ReadFormAsync();

            var name = form["Name"].FirstOrDefault() ?? "";
            var sourceLanguageId = int.TryParse(form["SourceLanguageId"].FirstOrDefault(), out var sid) ? sid : 0;
            var targetLanguageId = int.TryParse(form["TargetLanguageId"].FirstOrDefault(), out var tid) ? tid : 0;
            var rawVocabulary = form["RawVocabulary"].FirstOrDefault() ?? "";

            var request = new UpdateVocabularyListRequest(name, sourceLanguageId, targetLanguageId, rawVocabulary);
            await listService.UpdateAsync(id, userId, request);

            return Results.Redirect("/");
        }).RequireAuthorization().DisableAntiforgery();

        return app;
    }
}
