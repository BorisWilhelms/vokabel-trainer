using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Components;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<VocabularyListService>();
builder.Services.AddScoped<TrainingService>();
builder.Services.AddScoped<LeitnerService>();
builder.Services.AddScoped<ProgressService>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapPost("/training/{sessionId:int}/submit", async (int sessionId, HttpContext context, TrainingService trainingService) =>
{
    var form = await context.Request.ReadFormAsync();
    var action = form["Action"].FirstOrDefault();
    var mode = form["Mode"].FirstOrDefault() ?? "";

    if (string.Equals(action, "abort", StringComparison.OrdinalIgnoreCase))
    {
        await trainingService.AbortSessionAsync(sessionId);
        return Results.Redirect($"/training/result/{sessionId}");
    }

    var answer = form["Answer"].FirstOrDefault() ?? "";
    var vocabIdStr = form["VocabularyId"].FirstOrDefault();
    var directionStr = form["QuestionDirection"].FirstOrDefault();

    if (int.TryParse(vocabIdStr, out var vocabId) && int.TryParse(directionStr, out var dirInt))
    {
        var direction = (Direction)dirInt;
        var feedback = await trainingService.SubmitAnswerAsync(sessionId, vocabId, direction, answer);

        if (feedback.SessionComplete)
        {
            return Results.Redirect($"/training/result/{sessionId}");
        }

        var correct = feedback.IsCorrect ? "1" : "0";
        var correctAnswers = Uri.EscapeDataString(string.Join(", ", feedback.CorrectAnswers));
        var prompt = Uri.EscapeDataString(form["PreviousPrompt"].FirstOrDefault() ?? "");
        var givenAnswer = Uri.EscapeDataString(answer);

        return Results.Redirect(
            $"/training/{sessionId}?mode={mode}&fc={correct}&fa={correctAnswers}&fp={prompt}&fg={givenAnswer}");
    }

    return Results.Redirect($"/training/{sessionId}?mode={mode}");
}).DisableAntiforgery().RequireAuthorization();

app.MapRazorComponents<App>();
app.Run();
