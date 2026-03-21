using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages.Admin;
using VokabelTrainer.Api.Models.Languages;
using VokabelTrainer.Api.Models.Users;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        app.MapGet("/admin/users", async (UserService userService) =>
        {
            var users = await userService.GetAllAsync();

            return new RazorComponentResult<UserManagement>(new
            {
                Users = users
            });
        }).RequireAuthorization("AdminOnly");

        app.MapGet("/admin/languages", async (LanguageService languageService, string? edit, string? error) =>
        {
            var languages = await languageService.GetAllAsync();

            LanguageDto? editLanguage = null;
            if (int.TryParse(edit, out var editId))
            {
                editLanguage = languages.FirstOrDefault(l => l.Id == editId);
            }

            string? errorMessage = error switch
            {
                "required" => "Code und Name sind erforderlich.",
                "in-use" => "Sprache kann nicht geloescht werden, da sie noch verwendet wird.",
                _ => null
            };

            return new RazorComponentResult<LanguageManagement>(new
            {
                Languages = languages,
                EditLanguage = editLanguage,
                ErrorMessage = errorMessage
            });
        }).RequireAuthorization("AdminOnly");

        app.MapPost("/admin/users", async (HttpContext ctx, UserService userService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var username = form["NewUsername"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(username))
            {
                await userService.CreateAsync(new CreateUserRequest(username));
            }

            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/admin/users/{id:int}/reset", async (int id, UserService userService) =>
        {
            await userService.ResetPasswordAsync(id);
            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/admin/users/{id:int}/delete", async (int id, UserService userService) =>
        {
            await userService.DeleteAsync(id);
            return Results.Redirect("/admin/users");
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        app.MapPost("/admin/languages", async (HttpContext ctx, LanguageService languageService) =>
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

        app.MapPost("/admin/languages/{id:int}", async (int id, HttpContext ctx, LanguageService languageService) =>
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

        app.MapPost("/admin/languages/{id:int}/delete", async (int id, LanguageService languageService) =>
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
