# Vokabel Trainer

Vocabulary trainer web app for a 14-year-old student (Gymnasium, 9th grade). Focus on Latin and English. Session-based Leitner system for spaced repetition.

## Tech Stack

- .NET 10, C#
- ASP.NET Core Minimal API
- Razor Components as **pure HTML templates** (no `@page`, no `@inject`, no logic in components)
- EF Core + SQLite
- Bulma CSS + HTMX
- `Directory.Build.props`: Nullable enabled, TreatWarningsAsErrors, ImplicitUsings

## Architecture

**All routes are Minimal API endpoints.** Razor Components are only used as templates via `RazorComponentResult<T>`. There is no Blazor SSR routing, no `MapRazorComponents`, no Router.

### Pattern

```csharp
// GET: load data, return rendered component
app.MapGet("/", async (SomeService svc, HttpContext ctx) =>
{
    var data = await svc.LoadAsync(ctx.GetUserId());
    return new RazorComponentResult<MyPage>(new { Data = data, IsAdmin = ctx.User.IsInRole("Admin") });
}).RequireAuthorization();

// POST: process form, redirect (PRG pattern)
app.MapPost("/something", async (HttpContext ctx, SomeService svc) =>
{
    var form = await ctx.Request.ReadFormAsync();
    // process...
    return Results.Redirect("/");
}).RequireAuthorization().DisableAntiforgery();
```

### Component pattern

Components wrap content in `<PageLayout>` and only have `[Parameter]` properties:

```razor
<PageLayout IsAdmin="@IsAdmin">
    <h1 class="title">Page Title</h1>
    <!-- content -->
</PageLayout>

@code {
    [Parameter] public SomeDto Data { get; set; } = default!;
    [Parameter] public bool IsAdmin { get; set; }
}
```

Forms use plain HTML `<form method="post" action="/endpoint">`. No `EditForm`, no `@formname`, no `[SupplyParameterFromForm]`.

## Project Structure

```
src/VokabelTrainer.Api/
  Program.cs                    — DI, middleware, auto-migrate, language seed
  Endpoints/                    — Minimal API route handlers (Auth, Dashboard, List, Training, Progress, Admin)
  Services/                     — Business logic (Auth, Language, User, VocabularyList, VocabularyParser, Training, Leitner, Progress)
  Data/
    AppDbContext.cs              — EF Core context with Fluent API config
    Entities/                   — User, Language, VocabularyList, Vocabulary, BoxEntry, TrainingSession, TrainingAnswer
    Migrations/
  Models/                       — DTOs and enums (moved here from former Shared project)
  Components/
    PageLayout.razor            — Full HTML document wrapper (head, navbar, body, scripts)
    Pages/                      — Page components (pure templates)
    Shared/                     — Reusable components (BoxDistribution, LanguageFlag, LeitnerExplanation)
  wwwroot/
    css/app.css                 — Custom design system (light + dark theme)
    manifest.webmanifest        — PWA manifest
    service-worker.js

tests/VokabelTrainer.Api.Tests/
  Services/                     — Unit tests for services (36 tests)
  Helpers/TestDbContextFactory.cs

docker/
  Dockerfile                    — Multi-stage build (SDK → Alpine runtime)
  docker-compose.yml            — Compose with SQLite volume
  .dockerignore
```

## Running

```bash
# Development
dotnet run --project src/VokabelTrainer.Api

# Tests
dotnet test

# Docker
docker compose -f docker/docker-compose.yml up -d
```

The app auto-migrates the database and seeds default languages (Latein, Deutsch, Englisch) on startup. First user to log in becomes Admin.

## Key Design Decisions

- **Session-based Leitner** (not time-based): Box determines how many sessions until next review, not days. Works for irregular study patterns.
- **Translations as JSON array** in Vocabulary entity. Parsed from user input where `=` separates term from translations, and `,`, `;`, `|` are translation separators.
- **Cookie auth** with DB validation on every request (`OnValidatePrincipal`). No ASP.NET Identity.
- **Two training modes**: SinglePass (each vocab once) and Endlos (wrong answers return to pool until all correct).
- **MaxVocabulary** limits how many distinct vocab are asked per session.
- **Dark/Light theme**: Bulma v1 auto-detects OS preference. Toggle in navbar saves to localStorage.

## Conventions

- Don't use compound commands (`&&`, `;`) in bash
- Don't add co-author to git commits
- Keep components as pure templates — all logic in Endpoints and Services
- POST endpoints use `.DisableAntiforgery()` (plain HTML forms, no Blazor form system)
- Use `ctx.GetUserId()` extension method (defined in `Endpoints/HttpContextExtensions.cs`)
- Pass `IsAdmin = ctx.User.IsInRole("Admin")` to every page that shows the navbar
