using VokabelTrainer.Api.Models.Languages;
using VokabelTrainer.Api.Models.Users;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        app.MapPost("/form/admin/users", async (HttpContext ctx, UserService userService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var username = form["NewUsername"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(username))
            {
                await userService.CreateAsync(new CreateUserRequest(username));
            }

            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/form/admin/users/{id:int}/reset", async (int id, UserService userService) =>
        {
            await userService.ResetPasswordAsync(id);
            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/form/admin/users/{id:int}/delete", async (int id, UserService userService) =>
        {
            await userService.DeleteAsync(id);
            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/form/admin/languages", async (HttpContext ctx, LanguageService languageService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var code = form["FormCode"].FirstOrDefault();
            var displayName = form["FormDisplayName"].FirstOrDefault();
            var flagSvg = form["FormFlagSvg"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName))
            {
                return Results.Redirect("/admin/languages?error=required");
            }

            await languageService.CreateAsync(new CreateLanguageRequest(code, displayName, flagSvg));
            return Results.Redirect("/admin/languages");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/form/admin/languages/{id:int}", async (int id, HttpContext ctx, LanguageService languageService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var code = form["FormCode"].FirstOrDefault();
            var displayName = form["FormDisplayName"].FirstOrDefault();
            var flagSvg = form["FormFlagSvg"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName))
            {
                return Results.Redirect($"/admin/languages?edit={id}&error=required");
            }

            await languageService.UpdateAsync(id, new UpdateLanguageRequest(code, displayName, flagSvg));
            return Results.Redirect("/admin/languages");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/form/admin/languages/{id:int}/delete", async (int id, LanguageService languageService) =>
        {
            var success = await languageService.DeleteAsync(id);
            if (!success)
            {
                return Results.Redirect("/admin/languages?error=in-use");
            }
            return Results.Redirect("/admin/languages");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        return app;
    }
}
