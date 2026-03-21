# Vokabel Trainer

Vocabulary trainer web app for a 14-year-old student (Gymnasium, 9th grade). Focus on Latin and English. Session-based Leitner system for spaced repetition.

## Tech Stack

- .NET 10, C#
- ASP.NET Core Minimal API
- Razor Components as **pure HTML templates** (no `@page`, no `@inject`, no logic in components)
- EF Core + SQLite
- Bulma CSS + HTMX
- OpenRouter API for AI features (OCR, hints, flag generation)
- `Directory.Build.props`: Nullable enabled, TreatWarningsAsErrors, ImplicitUsings

## Architecture

**All routes are Minimal API endpoints.** Razor Components are only used as templates via `RazorComponentResult<T>`. There is no Blazor SSR routing, no `MapRazorComponents`, no Router.

### Patterns

**GET endpoints** — load data, return full page:
```csharp
app.MapGet("/", async (SomeService svc, HttpContext ctx) =>
{
    var data = await svc.LoadAsync(ctx.GetUserId());
    return new RazorComponentResult<MyPage>(new { Data = data, IsAdmin = ctx.User.IsInRole("Admin") });
}).RequireAuthorization();
```

**POST endpoints (navigation)** — process form, redirect (PRG pattern):
```csharp
app.MapPost("/something", async (HttpContext ctx, SomeService svc) =>
{
    var form = await ctx.Request.ReadFormAsync();
    // process...
    return Results.Redirect("/");
}).RequireAuthorization().DisableAntiforgery();
```

**POST endpoints (HTMX partial)** — process form, return partial content:
```csharp
app.MapPost("/training/{id}/submit", async (...) =>
{
    // process...
    // For redirects from HTMX: use HX-Redirect header
    ctx.Response.Headers["HX-Redirect"] = "/training/result/1";
    return Results.Ok();
    // For partial updates: return content component (no PageLayout wrapper)
    return new RazorComponentResult<TrainingContent>(new { ... });
}).RequireAuthorization().DisableAntiforgery();
```

### Component patterns

**Full page** — wraps content in `<PageLayout>`, used by GET endpoints:
```razor
<PageLayout IsAdmin="@IsAdmin">
    <h1 class="title">Page Title</h1>
</PageLayout>
@code {
    [Parameter] public bool IsAdmin { get; set; }
}
```

**Content partial** — no `<PageLayout>`, used by HTMX POST endpoints:
```razor
<!-- just the inner HTML, swapped into #content by HTMX -->
<div>...</div>
@code {
    [Parameter] public SomeDto Data { get; set; } = default!;
}
```

**Forms for navigation** use plain HTML: `<form method="post" action="/endpoint">`
**Forms for in-page updates** use HTMX: `<form hx-post="/endpoint" hx-target="#content" hx-swap="innerHTML">`

No `EditForm`, no `@formname`, no `[SupplyParameterFromForm]` anywhere.

## Project Structure

```
src/VokabelTrainer.Api/
  Program.cs                    — DI, middleware, auto-migrate, language seed, forwarded headers
  Endpoints/                    — Minimal API route handlers
    AuthEndpoints.cs            — Login, logout
    DashboardEndpoints.cs       — Dashboard (list overview)
    ListEndpoints.cs            — Create/edit lists, OCR upload
    TrainingEndpoints.cs        — Training flow (start, submit, abort, regenerate hint)
    ProgressEndpoints.cs        — Progress (per list + global)
    AdminEndpoints.cs           — User + language management, flag generation
    HttpContextExtensions.cs    — GetUserId() helper
  Services/
    AiService.cs                — OpenRouter API (OCR, hints, flag SVGs)
    AuthService.cs              — Login, admin setup
    TrainingService.cs          — Session management, answer checking, Leitner
    LeitnerService.cs           — Box promotion/demotion, intervals
    VocabularyListService.cs    — CRUD for lists
    VocabularyParser.cs         — Parses "term = translation" format
    LanguageService.cs          — CRUD for languages
    UserService.cs              — CRUD for users
    ProgressService.cs          — Stats, box distribution, problem vocab
  Data/
    AppDbContext.cs              — EF Core context with Fluent API config
    Entities/                   — User, Language, VocabularyList, Vocabulary, BoxEntry, TrainingSession, TrainingAnswer
    Migrations/
  Models/                       — DTOs and enums
  Components/
    PageLayout.razor            — Full HTML document (head, navbar, body, HTMX, theme toggle)
    Pages/                      — Full page components (wrapped in PageLayout)
      TrainingContent.razor     — Training partial (used by HTMX POST endpoints)
    Shared/                     — BoxDistribution, LanguageFlag, LeitnerExplanation
  wwwroot/
    css/app.css                 — Design system (light + dark theme, CSS variables)
    manifest.webmanifest        — PWA manifest
    service-worker.js

tests/VokabelTrainer.Api.Tests/
  Services/                     — Unit tests for services (36 tests)
  Helpers/TestDbContextFactory.cs

docker/
  Dockerfile                    — Multi-stage build (SDK → Alpine runtime)
  docker-compose.yml
  .dockerignore

.github/workflows/build.yml    — CI: test + build + push to GHCR
```

## Running

```bash
# Development
dotnet run --project src/VokabelTrainer.Api

# Tests
dotnet test

# Docker (local)
docker compose -f docker/docker-compose.yml up -d
```

The app auto-migrates the database and seeds default languages (Latein, Deutsch, Englisch) on startup. First user to log in becomes Admin.

## Key Design Decisions

- **Session-based Leitner** (not time-based): Box determines how many sessions until next review, not days. Works for irregular study patterns.
- **Translations as JSON array** in Vocabulary entity. Parsed from user input where `=` separates term from translations, and `,`, `;`, `|` are translation separators.
- **Cookie auth** with DB validation on every request (`OnValidatePrincipal`). No ASP.NET Identity.
- **Two training modes**: SinglePass (each vocab once) and Endlos (wrong answers return to pool until all correct).
- **MaxVocabulary** limits how many distinct vocab are asked per session.
- **Response time tracking**: Measured client-side (JS timer), stored per answer, >60s excluded from averages (AFK).
- **AI hints**: Generated on first wrong answer via OpenRouter API, stored permanently on Vocabulary entity. Can be regenerated.
- **Dark/Light theme**: Bulma v1 auto-detects OS preference. Toggle in navbar saves to localStorage.
- **HTMX for training flow**: Answer submission and hint regeneration use HTMX partial swaps (no page reload). Other forms use standard POST+redirect.
- **Forwarded headers**: Enabled for reverse proxy (Traefik) HTTPS support.

## AI Features (OpenRouter)

Configured in `appsettings.json` (use env vars for secrets):
```json
"OpenRouter": {
    "ApiKey": "",
    "VisionModel": "google/gemini-2.5-flash",
    "TextModel": "google/gemini-2.5-flash"
}
```

- **Photo OCR** — Extract vocabulary from photos of book pages (`POST /lists/ocr`)
- **Memory hints** — Generate mnemonics for wrong answers (etymological connections preferred)
- **Flag SVG generation** — Generate country flag SVGs for languages

## Conventions

- Don't use compound commands (`&&`, `;`) in bash
- Don't add co-author to git commits
- Keep components as pure templates — all logic in Endpoints and Services
- POST endpoints use `.DisableAntiforgery()` (plain HTML forms, no Blazor form system)
- Use `ctx.GetUserId()` extension method (defined in `Endpoints/HttpContextExtensions.cs`)
- Pass `IsAdmin = ctx.User.IsInRole("Admin")` to every full page component
- HTMX partials swap into `#content` div in PageLayout
- Use `HX-Redirect` header for navigation from HTMX POST endpoints
