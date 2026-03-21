using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/form/login", async (HttpContext ctx, AuthService authService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var username = form["Username"].FirstOrDefault() ?? "";
            var password = form["Password"].FirstOrDefault() ?? "";
            var passwordConfirmation = form["PasswordConfirmation"].FirstOrDefault() ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Redirect("/login?error=credentials");
            }

            var isSetup = await authService.NeedsInitialSetupAsync();

            if (isSetup && password != passwordConfirmation)
            {
                return Results.Redirect("/login?error=mismatch");
            }

            var result = await authService.LoginOrSetupAsync(username, password);
            if (result is null)
            {
                return Results.Redirect("/login?error=invalid");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
                new(ClaimTypes.Name, result.Username),
                new(ClaimTypes.Role, result.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.MapPost("/form/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        }).DisableAntiforgery();

        return app;
    }
}
