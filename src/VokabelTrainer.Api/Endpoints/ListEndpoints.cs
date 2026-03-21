using VokabelTrainer.Api.Models.Lists;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class ListEndpoints
{
    public static WebApplication MapListEndpoints(this WebApplication app)
    {
        app.MapPost("/form/lists", async (HttpContext ctx, VocabularyListService listService) =>
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

        app.MapPost("/form/lists/{id:int}", async (int id, HttpContext ctx, VocabularyListService listService) =>
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
