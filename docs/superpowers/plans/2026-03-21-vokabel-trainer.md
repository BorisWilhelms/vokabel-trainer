# Vokabel Trainer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a mobile-first vocabulary trainer web app with session-based Leitner system, supporting Latin and English.

**Architecture:** ASP.NET Core Web API backend with EF Core + SQLite, Blazor WASM PWA frontend, shared DTO project. All business logic in API services, frontend is a pure API consumer.

**Tech Stack:** .NET 10, ASP.NET Core, Blazor WASM, EF Core, SQLite, MudBlazor (UI-Komponentenbibliothek inkl. Charts), IStringLocalizer for localization.

**Compiler Settings:** Gemeinsame Properties (TargetFramework, Nullable, TreatWarningsAsErrors) in `Directory.Build.props` im Solution-Root.

**Spec:** `docs/superpowers/specs/2026-03-21-vokabel-trainer-design.md`

---

## File Structure

```
VokabelTrainer.sln
Directory.Build.props              # Shared: TargetFramework, Nullable, TreatWarningsAsErrors

src/VokabelTrainer.Shared/
  VokabelTrainer.Shared.csproj
  Models/
    UserRole.cs                  # enum: Admin, User
    TrainingMode.cs              # enum: SinglePass, Endlos
    Direction.cs                 # enum: SourceToTarget, TargetToSource
  Dtos/
    Auth/
      LoginRequest.cs
      SetPasswordRequest.cs
      AuthResponse.cs
    Languages/
      LanguageDto.cs
      CreateLanguageRequest.cs
      UpdateLanguageRequest.cs
    Users/
      UserDto.cs
      CreateUserRequest.cs
    Lists/
      VocabularyListDto.cs
      VocabularyListSummaryDto.cs
      CreateVocabularyListRequest.cs
      UpdateVocabularyListRequest.cs
    Training/
      StartSessionRequest.cs
      TrainingQuestionDto.cs
      SubmitAnswerRequest.cs
      SubmitAnswerResponse.cs
      SessionResultDto.cs
    Progress/
      ListProgressDto.cs
      SessionHistoryEntryDto.cs
      ProblemVocabularyDto.cs

src/VokabelTrainer.Api/
  VokabelTrainer.Api.csproj
  Program.cs
  appsettings.json
  Data/
    AppDbContext.cs
    Entities/
      User.cs
      Language.cs
      VocabularyList.cs
      Vocabulary.cs
      BoxEntry.cs
      TrainingSession.cs
      TrainingAnswer.cs
    Migrations/                  # EF Core auto-generated
  Services/
    AuthService.cs
    LanguageService.cs
    UserService.cs
    VocabularyListService.cs
    VocabularyParser.cs
    TrainingService.cs
    LeitnerService.cs
    ProgressService.cs
  Controllers/
    AuthController.cs
    LanguagesController.cs
    UsersController.cs
    VocabularyListsController.cs
    TrainingController.cs
    ProgressController.cs

src/VokabelTrainer.Client/
  VokabelTrainer.Client.csproj
  wwwroot/
    index.html
    manifest.json
    service-worker.js
    css/
      app.css
  Program.cs
  App.razor
  Routes.razor
  Layout/
    MainLayout.razor
    NavBar.razor
  Services/
    ApiClient.cs
    AuthStateProvider.cs
  Pages/
    Login.razor
    Dashboard.razor
    ListEditor.razor
    TrainingStart.razor
    Training.razor
    SessionResult.razor
    Progress.razor
    Admin/
      UserManagement.razor
      LanguageManagement.razor
  Components/
    BoxDistribution.razor
    LanguageFlag.razor
    LeitnerExplanation.razor
  Resources/
    Pages/*.de.resx             # German resource files per page
  _Imports.razor

tests/VokabelTrainer.Api.Tests/
  VokabelTrainer.Api.Tests.csproj
  Services/
    VocabularyParserTests.cs
    LeitnerServiceTests.cs
    TrainingServiceTests.cs
    AuthServiceTests.cs
    ProgressServiceTests.cs
  Helpers/
    TestDbContextFactory.cs
```

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `VokabelTrainer.sln`
- Create: `src/VokabelTrainer.Shared/VokabelTrainer.Shared.csproj`
- Create: `src/VokabelTrainer.Api/VokabelTrainer.Api.csproj`
- Create: `src/VokabelTrainer.Client/VokabelTrainer.Client.csproj`
- Create: `tests/VokabelTrainer.Api.Tests/VokabelTrainer.Api.Tests.csproj`
- Create: `.gitignore`

- [ ] **Step 1: Create solution and projects**

```bash
dotnet new sln -n VokabelTrainer
dotnet new classlib -n VokabelTrainer.Shared -o src/VokabelTrainer.Shared -f net10.0
dotnet new webapi -n VokabelTrainer.Api -o src/VokabelTrainer.Api -f net10.0
dotnet new blazorwasm -n VokabelTrainer.Client -o src/VokabelTrainer.Client -f net10.0 --pwa
dotnet new xunit -n VokabelTrainer.Api.Tests -o tests/VokabelTrainer.Api.Tests -f net10.0
```

- [ ] **Step 2: Add projects to solution and set up references**

```bash
dotnet sln add src/VokabelTrainer.Shared/VokabelTrainer.Shared.csproj
dotnet sln add src/VokabelTrainer.Api/VokabelTrainer.Api.csproj
dotnet sln add src/VokabelTrainer.Client/VokabelTrainer.Client.csproj
dotnet sln add tests/VokabelTrainer.Api.Tests/VokabelTrainer.Api.Tests.csproj

cd src/VokabelTrainer.Api
dotnet add reference ../VokabelTrainer.Shared/VokabelTrainer.Shared.csproj

cd ../VokabelTrainer.Client
dotnet add reference ../VokabelTrainer.Shared/VokabelTrainer.Shared.csproj

# API hosts the Blazor WASM client
cd ../VokabelTrainer.Api
dotnet add reference ../VokabelTrainer.Client/VokabelTrainer.Client.csproj

cd ../../tests/VokabelTrainer.Api.Tests
dotnet add reference ../../src/VokabelTrainer.Api/VokabelTrainer.Api.csproj
dotnet add reference ../../src/VokabelTrainer.Shared/VokabelTrainer.Shared.csproj
```

- [ ] **Step 3: Add NuGet packages**

```bash
# API project
cd src/VokabelTrainer.Api
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.AspNetCore.Authentication.Cookies
dotnet add package Microsoft.AspNetCore.Components.WebAssembly.Server
dotnet add package BCrypt.Net-Next

# Client project
cd ../VokabelTrainer.Client
dotnet add package MudBlazor

# Test project
cd ../../tests/VokabelTrainer.Api.Tests
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package FluentAssertions
```

- [ ] **Step 4: Create Directory.Build.props**

Create `Directory.Build.props` im Solution-Root mit gemeinsamen Properties:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Aus allen vier `.csproj`-Dateien `TargetFramework`, `Nullable`, `ImplicitUsings` und ggf. `TreatWarningsAsErrors` entfernen — wird automatisch von `Directory.Build.props` geerbt. Sicherstellen dass der Build danach warning-frei ist.

- [ ] **Step 5: Add .gitignore**

```bash
dotnet new gitignore
```

Append `.superpowers/` to `.gitignore`.

- [ ] **Step 5: Verify build**

```bash
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Delete template boilerplate**

Remove auto-generated template files:
- `src/VokabelTrainer.Shared/Class1.cs`
- `src/VokabelTrainer.Api/Controllers/WeatherForecastController.cs` (if present)
- `tests/VokabelTrainer.Api.Tests/UnitTest1.cs`

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "scaffold: solution with Api, Client, Shared, and test projects"
```

---

## Task 2: Shared Enums and DTOs

**Files:**
- Create: `src/VokabelTrainer.Shared/Models/UserRole.cs`
- Create: `src/VokabelTrainer.Shared/Models/TrainingMode.cs`
- Create: `src/VokabelTrainer.Shared/Models/Direction.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Auth/LoginRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Auth/SetPasswordRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Auth/AuthResponse.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Languages/LanguageDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Languages/CreateLanguageRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Languages/UpdateLanguageRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Users/UserDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Users/CreateUserRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Lists/VocabularyListDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Lists/VocabularyListSummaryDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Lists/CreateVocabularyListRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Lists/UpdateVocabularyListRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Training/StartSessionRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Training/TrainingQuestionDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Training/SubmitAnswerRequest.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Training/SubmitAnswerResponse.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Training/SessionResultDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Progress/ListProgressDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Progress/SessionHistoryEntryDto.cs`
- Create: `src/VokabelTrainer.Shared/Dtos/Progress/ProblemVocabularyDto.cs`

- [ ] **Step 1: Create enums**

```csharp
// Models/UserRole.cs
namespace VokabelTrainer.Shared.Models;
public enum UserRole { User, Admin }

// Models/TrainingMode.cs
namespace VokabelTrainer.Shared.Models;
public enum TrainingMode { SinglePass, Endlos }

// Models/Direction.cs
namespace VokabelTrainer.Shared.Models;
public enum Direction { SourceToTarget, TargetToSource }
```

- [ ] **Step 2: Create Auth DTOs**

```csharp
// Dtos/Auth/LoginRequest.cs
namespace VokabelTrainer.Shared.Dtos.Auth;
public record LoginRequest(string Username, string Password);

// Dtos/Auth/SetPasswordRequest.cs
namespace VokabelTrainer.Shared.Dtos.Auth;
public record SetPasswordRequest(string Username, string Password, string PasswordConfirmation);

// Dtos/Auth/AuthResponse.cs
namespace VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Models;
public record AuthResponse(int UserId, string Username, UserRole Role, bool RequiresPasswordSetup);
```

- [ ] **Step 3: Create Language DTOs**

```csharp
// Dtos/Languages/LanguageDto.cs
namespace VokabelTrainer.Shared.Dtos.Languages;
public record LanguageDto(int Id, string Code, string DisplayName, string? FlagSvg);

// Dtos/Languages/CreateLanguageRequest.cs
namespace VokabelTrainer.Shared.Dtos.Languages;
public record CreateLanguageRequest(string Code, string DisplayName, string? FlagSvg);

// Dtos/Languages/UpdateLanguageRequest.cs
namespace VokabelTrainer.Shared.Dtos.Languages;
public record UpdateLanguageRequest(string Code, string DisplayName, string? FlagSvg);
```

- [ ] **Step 4: Create User DTOs**

```csharp
// Dtos/Users/UserDto.cs
namespace VokabelTrainer.Shared.Dtos.Users;
using VokabelTrainer.Shared.Models;
public record UserDto(int Id, string Username, UserRole Role, bool IsInitialized);

// Dtos/Users/CreateUserRequest.cs
namespace VokabelTrainer.Shared.Dtos.Users;
public record CreateUserRequest(string Username);
```

- [ ] **Step 5: Create VocabularyList DTOs**

```csharp
// Dtos/Lists/VocabularyListDto.cs
namespace VokabelTrainer.Shared.Dtos.Lists;
public record VocabularyEntryDto(int Id, string Term, List<string> Translations);
public record VocabularyListDto(
    int Id, string Name,
    int SourceLanguageId, string SourceLanguageName,
    int TargetLanguageId, string TargetLanguageName,
    List<VocabularyEntryDto> Entries);

// Dtos/Lists/VocabularyListSummaryDto.cs
namespace VokabelTrainer.Shared.Dtos.Lists;
public record BoxDistributionDto(int Box1, int Box2, int Box3, int Box4, int Box5);
public record VocabularyListSummaryDto(
    int Id, string Name,
    int SourceLanguageId, string SourceLanguageName, string? SourceFlagSvg,
    int TargetLanguageId, string TargetLanguageName, string? TargetFlagSvg,
    int VocabularyCount, BoxDistributionDto? BoxDistribution);

// Dtos/Lists/CreateVocabularyListRequest.cs
namespace VokabelTrainer.Shared.Dtos.Lists;
public record CreateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);

// Dtos/Lists/UpdateVocabularyListRequest.cs
namespace VokabelTrainer.Shared.Dtos.Lists;
public record UpdateVocabularyListRequest(string Name, int SourceLanguageId, int TargetLanguageId, string RawVocabulary);
```

- [ ] **Step 6: Create Training DTOs**

```csharp
// Dtos/Training/StartSessionRequest.cs
namespace VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Models;
public record StartSessionRequest(int? ListId, TrainingMode Mode, int? MaxVocabulary);

// Dtos/Training/TrainingQuestionDto.cs
namespace VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Models;
public record TrainingQuestionDto(
    int SessionId, int VocabularyId,
    string Prompt, Direction Direction,
    string SourceLanguageName, string? SourceFlagSvg,
    string TargetLanguageName, string? TargetFlagSvg,
    int CurrentIndex, int TotalCount);

// Dtos/Training/SubmitAnswerRequest.cs
namespace VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Models;
public record SubmitAnswerRequest(int SessionId, int VocabularyId, Direction Direction, string Answer);

// Dtos/Training/SubmitAnswerResponse.cs
namespace VokabelTrainer.Shared.Dtos.Training;
public record SubmitAnswerResponse(
    bool IsCorrect, List<string> CorrectAnswers,
    int NewBox, bool SessionComplete);

// Dtos/Training/SessionResultDto.cs
namespace VokabelTrainer.Shared.Dtos.Training;
public record WrongAnswerDto(string Term, List<string> CorrectTranslations, string GivenAnswer);
public record SessionResultDto(
    int SessionId, int TotalQuestions, int CorrectAnswers,
    double SuccessRate, List<WrongAnswerDto> WrongAnswers);
```

- [ ] **Step 7: Create Progress DTOs**

```csharp
// Dtos/Progress/ListProgressDto.cs
namespace VokabelTrainer.Shared.Dtos.Progress;
using VokabelTrainer.Shared.Dtos.Lists;
public record ListProgressDto(
    int ListId, string ListName,
    BoxDistributionDto BoxDistribution,
    int TotalSessions,
    List<SessionHistoryEntryDto> SessionHistory,
    List<ProblemVocabularyDto> ProblemVocabulary);

// Dtos/Progress/SessionHistoryEntryDto.cs
namespace VokabelTrainer.Shared.Dtos.Progress;
public record SessionHistoryEntryDto(int SessionId, DateTime Date, double SuccessRate);

// Dtos/Progress/ProblemVocabularyDto.cs
namespace VokabelTrainer.Shared.Dtos.Progress;
public record ProblemVocabularyDto(string Term, int TimesWrong, int CurrentBox);
```

- [ ] **Step 8: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add shared enums and DTOs"
```

---

## Task 3: EF Core Data Model and DbContext

**Files:**
- Create: `src/VokabelTrainer.Api/Data/Entities/User.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/Language.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/VocabularyList.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/Vocabulary.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/BoxEntry.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/TrainingSession.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/TrainingAnswer.cs`
- Create: `src/VokabelTrainer.Api/Data/AppDbContext.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Helpers/TestDbContextFactory.cs`

- [ ] **Step 1: Create entity classes**

```csharp
// Data/Entities/User.cs
namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsInitialized { get; set; }
    public UserRole Role { get; set; }
    public List<VocabularyList> VocabularyLists { get; set; } = [];
    public List<BoxEntry> BoxEntries { get; set; } = [];
    public List<TrainingSession> TrainingSessions { get; set; } = [];
}

// Data/Entities/Language.cs
namespace VokabelTrainer.Api.Data.Entities;

public class Language
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public string? FlagSvg { get; set; }
}

// Data/Entities/VocabularyList.cs
namespace VokabelTrainer.Api.Data.Entities;

public class VocabularyList
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Name { get; set; }
    public int SourceLanguageId { get; set; }
    public Language SourceLanguage { get; set; } = null!;
    public int TargetLanguageId { get; set; }
    public Language TargetLanguage { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public List<Vocabulary> Vocabularies { get; set; } = [];
}

// Data/Entities/Vocabulary.cs
namespace VokabelTrainer.Api.Data.Entities;

public class Vocabulary
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public VocabularyList List { get; set; } = null!;
    public required string Term { get; set; }
    public required string Translations { get; set; } // JSON array
    public List<BoxEntry> BoxEntries { get; set; } = [];
    public List<TrainingAnswer> TrainingAnswers { get; set; } = [];
}

// Data/Entities/BoxEntry.cs
namespace VokabelTrainer.Api.Data.Entities;

public class BoxEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int VocabularyId { get; set; }
    public Vocabulary Vocabulary { get; set; } = null!;
    public int Box { get; set; } = 1;
    public int SessionsUntilReview { get; set; }
}

// Data/Entities/TrainingSession.cs
namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Models;

public class TrainingSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? ListId { get; set; }
    public VocabularyList? List { get; set; }
    public TrainingMode Mode { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public List<TrainingAnswer> Answers { get; set; } = [];
}

// Data/Entities/TrainingAnswer.cs
namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Models;

public class TrainingAnswer
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public TrainingSession Session { get; set; } = null!;
    public int VocabularyId { get; set; }
    public Vocabulary Vocabulary { get; set; } = null!;
    public Direction Direction { get; set; }
    public required string GivenAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
}
```

- [ ] **Step 2: Create AppDbContext**

```csharp
// Data/AppDbContext.cs
namespace VokabelTrainer.Api.Data;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data.Entities;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<VocabularyList> VocabularyLists => Set<VocabularyList>();
    public DbSet<Vocabulary> Vocabularies => Set<Vocabulary>();
    public DbSet<BoxEntry> BoxEntries => Set<BoxEntry>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<TrainingAnswer> TrainingAnswers => Set<TrainingAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<Language>()
            .HasIndex(l => l.Code).IsUnique();

        modelBuilder.Entity<BoxEntry>()
            .HasIndex(b => new { b.UserId, b.VocabularyId }).IsUnique();

        modelBuilder.Entity<VocabularyList>()
            .HasOne(vl => vl.SourceLanguage)
            .WithMany()
            .HasForeignKey(vl => vl.SourceLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VocabularyList>()
            .HasOne(vl => vl.TargetLanguage)
            .WithMany()
            .HasForeignKey(vl => vl.TargetLanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Vocabulary>()
            .HasMany(v => v.BoxEntries)
            .WithOne(b => b.Vocabulary)
            .HasForeignKey(b => b.VocabularyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vocabulary>()
            .HasMany(v => v.TrainingAnswers)
            .WithOne(a => a.Vocabulary)
            .HasForeignKey(a => a.VocabularyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: Create TestDbContextFactory**

```csharp
// tests/Helpers/TestDbContextFactory.cs
namespace VokabelTrainer.Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
```

- [ ] **Step 4: Configure DbContext in Program.cs**

In `src/VokabelTrainer.Api/Program.cs`, register the DbContext with SQLite:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=vokabeltrainer.db"
  }
}
```

- [ ] **Step 5: Create initial migration**

```bash
cd src/VokabelTrainer.Api
dotnet ef migrations add InitialCreate
```

- [ ] **Step 6: Verify build and tests**

```bash
dotnet build
dotnet test
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add EF Core data model, DbContext, and initial migration"
```

---

## Task 4: VocabularyParser Service (TDD)

**Files:**
- Create: `src/VokabelTrainer.Api/Services/VocabularyParser.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/VocabularyParserTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Services/VocabularyParserTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Services;

public class VocabularyParserTests
{
    [Fact]
    public void Parse_SimpleEntry_ReturnsTermAndTranslations()
    {
        var result = VocabularyParser.Parse("res = Sache, Ding, Angelegenheit");
        result.Should().HaveCount(1);
        result[0].Term.Should().Be("res");
        result[0].Translations.Should().BeEquivalentTo(["Sache", "Ding", "Angelegenheit"]);
    }

    [Fact]
    public void Parse_MultipleLines_ReturnsAll()
    {
        var input = "res = Sache, Ding\namo = lieben, moegen";
        var result = VocabularyParser.Parse(input);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_SemicolonSeparator_SplitsTranslations()
    {
        var result = VocabularyParser.Parse("pax = Frieden; Ruhe");
        result[0].Translations.Should().BeEquivalentTo(["Frieden", "Ruhe"]);
    }

    [Fact]
    public void Parse_PipeSeparator_SplitsTranslations()
    {
        var result = VocabularyParser.Parse("rex = Koenig | Herrscher");
        result[0].Translations.Should().BeEquivalentTo(["Koenig", "Herrscher"]);
    }

    [Fact]
    public void Parse_MixedSeparators_SplitsAll()
    {
        var result = VocabularyParser.Parse("res = Sache, Ding; Angelegenheit | Vermoegen");
        result[0].Translations.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var result = VocabularyParser.Parse("  res  =  Sache ,  Ding  ");
        result[0].Term.Should().Be("res");
        result[0].Translations.Should().BeEquivalentTo(["Sache", "Ding"]);
    }

    [Fact]
    public void Parse_SkipsEmptyLines()
    {
        var input = "res = Sache\n\n\namo = lieben";
        var result = VocabularyParser.Parse(input);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_LineWithoutEquals_IsSkipped()
    {
        var result = VocabularyParser.Parse("this has no equals sign");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleTranslation_Works()
    {
        var result = VocabularyParser.Parse("bellum = Krieg");
        result[0].Translations.Should().BeEquivalentTo(["Krieg"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "VocabularyParserTests"
```

Expected: Compilation error — `VocabularyParser` does not exist.

- [ ] **Step 3: Implement VocabularyParser**

```csharp
// Services/VocabularyParser.cs
namespace VokabelTrainer.Api.Services;

public static class VocabularyParser
{
    public record ParsedEntry(string Term, List<string> Translations);

    private static readonly char[] TranslationSeparators = [',', ';', '|'];

    public static List<ParsedEntry> Parse(string rawInput)
    {
        var results = new List<ParsedEntry>();

        foreach (var line in rawInput.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
                continue;

            var term = line[..equalsIndex].Trim();
            var translationsRaw = line[(equalsIndex + 1)..];

            var translations = translationsRaw
                .Split(TranslationSeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (term.Length > 0 && translations.Count > 0)
                results.Add(new ParsedEntry(term, translations));
        }

        return results;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "VocabularyParserTests"
```

Expected: All 9 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add VocabularyParser with support for multiple separators"
```

---

## Task 5: AuthService (TDD)

**Files:**
- Create: `src/VokabelTrainer.Api/Services/AuthService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/AuthServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Services/AuthServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Shared.Models;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_EmptyDb_CreatesAdmin()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db);

        var result = await service.LoginOrSetupAsync("boris", "geheim123");

        result.Should().NotBeNull();
        result!.Role.Should().Be(UserRole.Admin);
        db.Users.Should().HaveCount(1);
        db.Users.First().IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("pw"), IsInitialized = true, Role = UserRole.Admin });
        await db.SaveChangesAsync();
        var service = new AuthService(db);

        var result = await service.LoginOrSetupAsync("unknown", "pw");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Login_UninitializedUser_SetsPassword()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);

        var result = await service.LoginOrSetupAsync("anna", "meinpasswort");

        result.Should().NotBeNull();
        result!.RequiresPasswordSetup.Should().BeFalse();
        db.Users.First().IsInitialized.Should().BeTrue();
        db.Users.First().PasswordHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", PasswordHash = BCrypt.Net.BCrypt.HashPassword("richtig"), IsInitialized = true, Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);

        var result = await service.LoginOrSetupAsync("anna", "richtig");

        result.Should().NotBeNull();
        result!.Username.Should().Be("anna");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "anna", PasswordHash = BCrypt.Net.BCrypt.HashPassword("richtig"), IsInitialized = true, Role = UserRole.User });
        await db.SaveChangesAsync();
        var service = new AuthService(db);

        var result = await service.LoginOrSetupAsync("anna", "falsch");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CheckNeedsSetup_EmptyDb_ReturnsTrue()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db);

        var result = await service.NeedsInitialSetupAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckNeedsSetup_WithUsers_ReturnsFalse()
    {
        using var db = TestDbContextFactory.Create();
        db.Users.Add(new User { Username = "admin", IsInitialized = true, Role = UserRole.Admin });
        await db.SaveChangesAsync();
        var service = new AuthService(db);

        var result = await service.NeedsInitialSetupAsync();

        result.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "AuthServiceTests"
```

Expected: Compilation error.

- [ ] **Step 3: Implement AuthService**

```csharp
// Services/AuthService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Models;

public class AuthService(AppDbContext db)
{
    public async Task<bool> NeedsInitialSetupAsync()
    {
        return !await db.Users.AnyAsync();
    }

    public async Task<AuthResponse?> LoginOrSetupAsync(string username, string password)
    {
        // First user ever becomes admin
        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsInitialized = true,
                Role = UserRole.Admin
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            return new AuthResponse(admin.Id, admin.Username, admin.Role, false);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null)
            return null;

        // Uninitialized user — set password
        if (!user.IsInitialized)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            user.IsInitialized = true;
            await db.SaveChangesAsync();
            return new AuthResponse(user.Id, user.Username, user.Role, false);
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return new AuthResponse(user.Id, user.Username, user.Role, false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "AuthServiceTests"
```

Expected: All 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add AuthService with admin auto-setup and password initialization"
```

---

## Task 6: LeitnerService (TDD)

**Files:**
- Create: `src/VokabelTrainer.Api/Services/LeitnerService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/LeitnerServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Services/LeitnerServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;

public class LeitnerServiceTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void GetInterval_ReturnsCorrectValue(int box, int expectedInterval)
    {
        LeitnerService.GetInterval(box).Should().Be(expectedInterval);
    }

    [Fact]
    public void PromoteBox_IncrementsAndSetsInterval()
    {
        var entry = new BoxEntry { Box = 2, SessionsUntilReview = 0 };

        LeitnerService.Promote(entry);

        entry.Box.Should().Be(3);
        entry.SessionsUntilReview.Should().Be(4); // interval for box 3
    }

    [Fact]
    public void PromoteBox_AtMax_StaysAtFive()
    {
        var entry = new BoxEntry { Box = 5, SessionsUntilReview = 0 };

        LeitnerService.Promote(entry);

        entry.Box.Should().Be(5);
        entry.SessionsUntilReview.Should().Be(16);
    }

    [Fact]
    public void DemoteBox_ResetsToOne()
    {
        var entry = new BoxEntry { Box = 4, SessionsUntilReview = 5 };

        LeitnerService.Demote(entry);

        entry.Box.Should().Be(1);
        entry.SessionsUntilReview.Should().Be(1); // interval for box 1
    }

    [Fact]
    public async Task EnsureBoxEntries_CreatesForNewVocabulary()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = Shared.Models.UserRole.User };
        var lang = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        list.Vocabularies.Add(new Vocabulary { Term = "res", Translations = "[\"Sache\"]" });
        list.Vocabularies.Add(new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" });
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        var service = new LeitnerService(db);
        await service.EnsureBoxEntriesAsync(user.Id, list.Id);

        db.BoxEntries.Should().HaveCount(2);
        db.BoxEntries.Should().AllSatisfy(b =>
        {
            b.Box.Should().Be(1);
            b.SessionsUntilReview.Should().Be(0);
        });
    }

    [Fact]
    public async Task DecrementSessionCounters_ReducesAll()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = Shared.Models.UserRole.User };
        var lang = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        var vocab1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        var vocab2 = new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" };
        list.Vocabularies.AddRange([vocab1, vocab2]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab1.Id, Box = 3, SessionsUntilReview = 3 });
        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab2.Id, Box = 2, SessionsUntilReview = 1 });
        await db.SaveChangesAsync();

        var service = new LeitnerService(db);
        await service.DecrementSessionCountersAsync(user.Id, list.Id);

        db.BoxEntries.First(b => b.VocabularyId == vocab1.Id).SessionsUntilReview.Should().Be(2);
        db.BoxEntries.First(b => b.VocabularyId == vocab2.Id).SessionsUntilReview.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "LeitnerServiceTests"
```

Expected: Compilation error.

- [ ] **Step 3: Implement LeitnerService**

```csharp
// Services/LeitnerService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;

public class LeitnerService(AppDbContext db)
{
    private static readonly Dictionary<int, int> Intervals = new()
    {
        { 1, 1 }, { 2, 2 }, { 3, 4 }, { 4, 8 }, { 5, 16 }
    };

    public static int GetInterval(int box) => Intervals[box];

    public static void Promote(BoxEntry entry)
    {
        entry.Box = Math.Min(entry.Box + 1, 5);
        entry.SessionsUntilReview = GetInterval(entry.Box);
    }

    public static void Demote(BoxEntry entry)
    {
        entry.Box = 1;
        entry.SessionsUntilReview = GetInterval(1);
    }

    public async Task EnsureBoxEntriesAsync(int userId, int listId)
    {
        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var existingVocabIds = await db.BoxEntries
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .Select(b => b.VocabularyId)
            .ToListAsync();

        var newEntries = vocabIds
            .Except(existingVocabIds)
            .Select(vid => new BoxEntry
            {
                UserId = userId,
                VocabularyId = vid,
                Box = 1,
                SessionsUntilReview = 0
            });

        db.BoxEntries.AddRange(newEntries);
        await db.SaveChangesAsync();
    }

    public async Task DecrementSessionCountersAsync(int userId, int listId)
    {
        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var entries = await db.BoxEntries
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .ToListAsync();

        foreach (var entry in entries)
            entry.SessionsUntilReview = Math.Max(0, entry.SessionsUntilReview - 1);

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "LeitnerServiceTests"
```

Expected: All 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add LeitnerService with box promotion, demotion, and session counters"
```

---

## Task 7: TrainingService (TDD)

**Files:**
- Create: `src/VokabelTrainer.Api/Services/TrainingService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/TrainingServiceTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Services/TrainingServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using System.Text.Json;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Shared.Models;

public class TrainingServiceTests
{
    private async Task<(AppDbContext db, int userId, int listId)> SetupTestDataAsync()
    {
        var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = UserRole.User, IsInitialized = true };
        var lang1 = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang1, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang1.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        list.Vocabularies.AddRange([
            new Vocabulary { Term = "res", Translations = JsonSerializer.Serialize(new[] { "Sache", "Ding" }) },
            new Vocabulary { Term = "amo", Translations = JsonSerializer.Serialize(new[] { "lieben" }) },
            new Vocabulary { Term = "bellum", Translations = JsonSerializer.Serialize(new[] { "Krieg" }) },
        ]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        // Create box entries (all due)
        foreach (var vocab in list.Vocabularies)
            db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = vocab.Id, Box = 1, SessionsUntilReview = 0 });
        await db.SaveChangesAsync();

        return (db, user.Id, list.Id);
    }

    [Fact]
    public async Task StartSession_CreatesSession()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);

        sessionId.Should().BeGreaterThan(0);
        var session = db.TrainingSessions.First();
        session.Mode.Should().Be(TrainingMode.SinglePass);
        session.ListId.Should().Be(listId);
    }

    [Fact]
    public async Task GetNextQuestion_ReturnsDueVocabulary()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        question.Should().NotBeNull();
        question!.Prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubmitAnswer_CorrectAnswer_ReturnsTrue()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        // Build correct answer based on direction
        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var correctAnswer = question!.Direction == Direction.SourceToTarget
            ? translations[0]
            : vocab.Term;

        var response = await service.SubmitAnswerAsync(sessionId, question.VocabularyId, question.Direction, correctAnswer);

        response.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_WrongAnswer_ReturnsFalse()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var response = await service.SubmitAnswerAsync(sessionId, question!.VocabularyId, question.Direction, "voellig falsch");

        response.IsCorrect.Should().BeFalse();
        response.CorrectAnswers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SubmitAnswer_CaseInsensitive()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var answer = question!.Direction == Direction.SourceToTarget
            ? translations[0].ToUpper()
            : vocab.Term.ToUpper();

        var response = await service.SubmitAnswerAsync(sessionId, question.VocabularyId, question.Direction, answer);

        response.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_CorrectPromotesBox()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        var question = await service.GetNextQuestionAsync(sessionId);

        var vocab = db.Vocabularies.First(v => v.Id == question!.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var answer = question!.Direction == Direction.SourceToTarget
            ? translations[0] : vocab.Term;

        await service.SubmitAnswerAsync(sessionId, question.VocabularyId, answer);

        var box = db.BoxEntries.First(b => b.VocabularyId == question.VocabularyId);
        box.Box.Should().Be(2);
    }

    [Fact]
    public async Task SubmitAnswer_WrongDemotesToBoxOne()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        // Move one vocab to box 3
        var firstVocab = db.Vocabularies.First();
        var boxEntry = db.BoxEntries.First(b => b.VocabularyId == firstVocab.Id);
        boxEntry.Box = 3;
        await db.SaveChangesAsync();

        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);
        // Keep getting questions until we get the one in box 3
        // For simplicity, just submit wrong for any question
        var question = await service.GetNextQuestionAsync(sessionId);
        await service.SubmitAnswerAsync(sessionId, question!.VocabularyId, question.Direction, "falsch");

        var box = db.BoxEntries.First(b => b.VocabularyId == question.VocabularyId);
        box.Box.Should().Be(1);
    }

    [Fact]
    public async Task GetSessionResult_ReturnsCorrectStats()
    {
        var (db, userId, listId) = await SetupTestDataAsync();
        var leitner = new LeitnerService(db);
        var service = new TrainingService(db, leitner);

        var sessionId = await service.StartSessionAsync(userId, listId, TrainingMode.SinglePass, null);

        // Answer all questions
        for (int i = 0; i < 3; i++)
        {
            var q = await service.GetNextQuestionAsync(sessionId);
            if (q == null) break;
            await service.SubmitAnswerAsync(sessionId, q.VocabularyId, q.Direction, i == 0 ? "falsch" : GetCorrectAnswer(db, q));
        }

        var result = await service.GetSessionResultAsync(sessionId);

        result.Should().NotBeNull();
        result!.TotalQuestions.Should().Be(3);
        result.CorrectAnswers.Should().Be(2);
        result.WrongAnswers.Should().HaveCount(1);
    }

    private static string GetCorrectAnswer(AppDbContext db, Shared.Dtos.Training.TrainingQuestionDto q)
    {
        var vocab = db.Vocabularies.First(v => v.Id == q.VocabularyId);
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        return q.Direction == Direction.SourceToTarget ? translations[0] : vocab.Term;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter "TrainingServiceTests"
```

Expected: Compilation error.

- [ ] **Step 3: Implement TrainingService**

```csharp
// Services/TrainingService.cs
namespace VokabelTrainer.Api.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Models;

public class TrainingService(AppDbContext db, LeitnerService leitner)
{
    private static readonly Random Rng = new();

    public async Task<int> StartSessionAsync(int userId, int? listId, TrainingMode mode, int? maxVocabulary)
    {
        // Ensure box entries exist
        if (listId.HasValue)
        {
            await leitner.EnsureBoxEntriesAsync(userId, listId.Value);
        }
        else
        {
            var listIds = await db.VocabularyLists
                .Where(l => l.UserId == userId)
                .Select(l => l.Id)
                .ToListAsync();
            foreach (var lid in listIds)
                await leitner.EnsureBoxEntriesAsync(userId, lid);
        }

        var session = new TrainingSession
        {
            UserId = userId,
            ListId = listId,
            Mode = mode,
            StartedAt = DateTime.UtcNow,
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    public async Task<TrainingQuestionDto?> GetNextQuestionAsync(int sessionId)
    {
        var session = await db.TrainingSessions
            .Include(s => s.List).ThenInclude(l => l!.SourceLanguage)
            .Include(s => s.List).ThenInclude(l => l!.TargetLanguage)
            .FirstAsync(s => s.Id == sessionId);

        var answeredVocabIds = await db.TrainingAnswers
            .Where(a => a.SessionId == sessionId)
            .Select(a => a.VocabularyId)
            .ToListAsync();

        // In Endlos mode, only exclude vocab that was answered correctly
        var correctlyAnsweredIds = session.Mode == TrainingMode.Endlos
            ? await db.TrainingAnswers
                .Where(a => a.SessionId == sessionId && a.IsCorrect)
                .Select(a => a.VocabularyId)
                .Distinct()
                .ToListAsync()
            : answeredVocabIds.Distinct().ToList();

        // Get due vocabulary, excluding already completed
        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIdsInList = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value)
                .Select(v => v.Id);
            query = query.Where(b => vocabIdsInList.Contains(b.VocabularyId));
        }

        var dueEntries = await query
            .Where(b => !correctlyAnsweredIds.Contains(b.VocabularyId))
            .OrderBy(b => b.Box)
            .ThenBy(b => Guid.NewGuid()) // random within same box
            .ToListAsync();

        // In Endlos mode, also avoid recently wrong-answered vocab (delay re-asking)
        if (session.Mode == TrainingMode.Endlos && dueEntries.Count > 1)
        {
            var recentWrongIds = await db.TrainingAnswers
                .Where(a => a.SessionId == sessionId && !a.IsCorrect)
                .OrderByDescending(a => a.AnsweredAt)
                .Take(3)
                .Select(a => a.VocabularyId)
                .ToListAsync();

            var delayed = dueEntries.Where(e => !recentWrongIds.Contains(e.VocabularyId)).ToList();
            if (delayed.Count > 0)
                dueEntries = delayed;
        }

        var nextEntry = dueEntries.FirstOrDefault();
        if (nextEntry is null)
            return null;

        var vocab = await db.Vocabularies
            .Include(v => v.List).ThenInclude(l => l.SourceLanguage)
            .Include(v => v.List).ThenInclude(l => l.TargetLanguage)
            .FirstAsync(v => v.Id == nextEntry.VocabularyId);

        var direction = Rng.Next(2) == 0 ? Direction.SourceToTarget : Direction.TargetToSource;
        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var prompt = direction == Direction.SourceToTarget ? vocab.Term : translations[Rng.Next(translations.Count)];

        var totalCount = session.Mode == TrainingMode.Endlos
            ? dueEntries.Count + correctlyAnsweredIds.Count
            : await CountTotalDueAsync(session) ;
        var currentIndex = correctlyAnsweredIds.Count + 1;

        return new TrainingQuestionDto(
            session.Id, vocab.Id, prompt, direction,
            vocab.List.SourceLanguage.DisplayName, vocab.List.SourceLanguage.FlagSvg,
            vocab.List.TargetLanguage.DisplayName, vocab.List.TargetLanguage.FlagSvg,
            currentIndex, totalCount);
    }

    public async Task<SubmitAnswerResponse> SubmitAnswerAsync(
        int sessionId, int vocabularyId, Direction direction, string answer)
    {
        var session = await db.TrainingSessions.FirstAsync(s => s.Id == sessionId);
        var vocab = await db.Vocabularies.FirstAsync(v => v.Id == vocabularyId);
        var boxEntry = await db.BoxEntries
            .FirstAsync(b => b.UserId == session.UserId && b.VocabularyId == vocabularyId);

        var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
        var trimmedAnswer = answer.Trim();

        // Direction-aware answer checking:
        // SourceToTarget: prompted with Term, answer must match a Translation
        // TargetToSource: prompted with a Translation, answer must match Term
        var isCorrect = direction == Direction.SourceToTarget
            ? translations.Any(t => string.Equals(t.Trim(), trimmedAnswer, StringComparison.OrdinalIgnoreCase))
            : string.Equals(vocab.Term.Trim(), trimmedAnswer, StringComparison.OrdinalIgnoreCase);

        var trainingAnswer = new TrainingAnswer
        {
            SessionId = sessionId,
            VocabularyId = vocabularyId,
            Direction = direction,
            GivenAnswer = answer,
            IsCorrect = isCorrect,
            AnsweredAt = DateTime.UtcNow
        };
        db.TrainingAnswers.Add(trainingAnswer);

        if (isCorrect)
        {
            LeitnerService.Promote(boxEntry);
            session.CorrectAnswers++;
        }
        else
        {
            LeitnerService.Demote(boxEntry);
        }
        session.TotalQuestions++;

        // Check if session is complete (no more due vocabulary)
        var sessionComplete = !await HasRemainingQuestionsAsync(session);

        if (sessionComplete)
        {
            session.CompletedAt = DateTime.UtcNow;
            // Decrement counters for affected lists
            if (session.ListId.HasValue)
                await leitner.DecrementSessionCountersAsync(session.UserId, session.ListId.Value);
            else
            {
                var listIds = await db.VocabularyLists
                    .Where(l => l.UserId == session.UserId)
                    .Select(l => l.Id).ToListAsync();
                foreach (var lid in listIds)
                    await leitner.DecrementSessionCountersAsync(session.UserId, lid);
            }
        }

        await db.SaveChangesAsync();

        var correctAnswers = direction == Direction.SourceToTarget
            ? translations : [vocab.Term];

        return new SubmitAnswerResponse(isCorrect, correctAnswers, boxEntry.Box, sessionComplete);
    }

    private async Task<bool> HasRemainingQuestionsAsync(TrainingSession session)
    {
        var correctlyAnsweredIds = session.Mode == TrainingMode.Endlos
            ? await db.TrainingAnswers
                .Where(a => a.SessionId == session.Id && a.IsCorrect)
                .Select(a => a.VocabularyId).Distinct().ToListAsync()
            : await db.TrainingAnswers
                .Where(a => a.SessionId == session.Id)
                .Select(a => a.VocabularyId).Distinct().ToListAsync();

        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIds = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value).Select(v => v.Id);
            query = query.Where(b => vocabIds.Contains(b.VocabularyId));
        }

        return await query.AnyAsync(b => !correctlyAnsweredIds.Contains(b.VocabularyId));
    }

    public async Task<SessionResultDto?> GetSessionResultAsync(int sessionId)
    {
        var session = await db.TrainingSessions
            .Include(s => s.Answers).ThenInclude(a => a.Vocabulary)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session is null) return null;

        var wrongAnswers = session.Answers
            .Where(a => !a.IsCorrect)
            .GroupBy(a => a.VocabularyId)
            .Select(g =>
            {
                var vocab = g.First().Vocabulary;
                var translations = JsonSerializer.Deserialize<List<string>>(vocab.Translations)!;
                return new WrongAnswerDto(vocab.Term, translations, g.Last().GivenAnswer);
            })
            .ToList();

        var successRate = session.TotalQuestions > 0
            ? (double)session.CorrectAnswers / session.TotalQuestions * 100
            : 0;

        return new SessionResultDto(
            session.Id, session.TotalQuestions, session.CorrectAnswers,
            Math.Round(successRate, 1), wrongAnswers);
    }

    public async Task AbortSessionAsync(int sessionId)
    {
        var session = await db.TrainingSessions.FirstAsync(s => s.Id == sessionId);
        session.CompletedAt = DateTime.UtcNow;

        if (session.ListId.HasValue)
            await leitner.DecrementSessionCountersAsync(session.UserId, session.ListId.Value);
        else
        {
            var listIds = await db.VocabularyLists
                .Where(l => l.UserId == session.UserId)
                .Select(l => l.Id).ToListAsync();
            foreach (var lid in listIds)
                await leitner.DecrementSessionCountersAsync(session.UserId, lid);
        }

        await db.SaveChangesAsync();
    }

    private async Task<int> CountTotalDueAsync(TrainingSession session)
    {
        var query = db.BoxEntries
            .Where(b => b.UserId == session.UserId && b.SessionsUntilReview <= 0);

        if (session.ListId.HasValue)
        {
            var vocabIds = db.Vocabularies
                .Where(v => v.ListId == session.ListId.Value)
                .Select(v => v.Id);
            query = query.Where(b => vocabIds.Contains(b.VocabularyId));
        }

        return await query.CountAsync();
    }
}
```

Note: The direction tracking is slightly simplified here. A refinement would be to store the question's direction in a session state cache, so SubmitAnswer knows exactly which direction was asked. For the initial implementation this works because we validate against both term and translations.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "TrainingServiceTests"
```

Expected: All 8 tests pass.

- [ ] **Step 5: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add TrainingService with session management, answer checking, and Leitner integration"
```

---

## Task 8: Remaining Backend Services

**Files:**
- Create: `src/VokabelTrainer.Api/Services/LanguageService.cs`
- Create: `src/VokabelTrainer.Api/Services/UserService.cs`
- Create: `src/VokabelTrainer.Api/Services/VocabularyListService.cs`
- Create: `src/VokabelTrainer.Api/Services/ProgressService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/ProgressServiceTests.cs`

- [ ] **Step 1: Implement LanguageService**

Simple CRUD, no complex logic:

```csharp
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
```

- [ ] **Step 2: Implement UserService**

```csharp
// Services/UserService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Users;
using VokabelTrainer.Shared.Models;

public class UserService(AppDbContext db)
{
    public async Task<List<UserDto>> GetAllAsync()
        => await db.Users
            .Select(u => new UserDto(u.Id, u.Username, u.Role, u.IsInitialized))
            .ToListAsync();

    public async Task<UserDto> CreateAsync(CreateUserRequest request)
    {
        var user = new User { Username = request.Username, Role = UserRole.User };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new UserDto(user.Id, user.Username, user.Role, user.IsInitialized);
    }

    public async Task<bool> ResetPasswordAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return false;
        user.PasswordHash = null;
        user.IsInitialized = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return false;
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return true;
    }
}
```

- [ ] **Step 3: Implement VocabularyListService**

```csharp
// Services/VocabularyListService.cs
namespace VokabelTrainer.Api.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Shared.Dtos.Lists;

public class VocabularyListService(AppDbContext db)
{
    public async Task<List<VocabularyListSummaryDto>> GetAllForUserAsync(int userId)
    {
        var lists = await db.VocabularyLists
            .Where(l => l.UserId == userId)
            .Include(l => l.SourceLanguage)
            .Include(l => l.TargetLanguage)
            .Include(l => l.Vocabularies)
            .ToListAsync();

        var result = new List<VocabularyListSummaryDto>();
        foreach (var list in lists)
        {
            var vocabIds = list.Vocabularies.Select(v => v.Id).ToList();
            var boxEntries = await db.BoxEntries
                .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
                .ToListAsync();

            BoxDistributionDto? dist = boxEntries.Count > 0
                ? new BoxDistributionDto(
                    boxEntries.Count(b => b.Box == 1),
                    boxEntries.Count(b => b.Box == 2),
                    boxEntries.Count(b => b.Box == 3),
                    boxEntries.Count(b => b.Box == 4),
                    boxEntries.Count(b => b.Box == 5))
                : null;

            result.Add(new VocabularyListSummaryDto(
                list.Id, list.Name,
                list.SourceLanguageId, list.SourceLanguage.DisplayName, list.SourceLanguage.FlagSvg,
                list.TargetLanguageId, list.TargetLanguage.DisplayName, list.TargetLanguage.FlagSvg,
                list.Vocabularies.Count, dist));
        }

        return result;
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
```

- [ ] **Step 4: Write ProgressService tests**

```csharp
// tests/Services/ProgressServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using System.Text.Json;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;
using VokabelTrainer.Shared.Models;

public class ProgressServiceTests
{
    [Fact]
    public async Task GetListProgress_ReturnsBoxDistribution()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = UserRole.User, IsInitialized = true };
        var lang1 = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang1, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang1.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        var v1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        var v2 = new Vocabulary { Term = "amo", Translations = "[\"lieben\"]" };
        list.Vocabularies.AddRange([v1, v2]);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v1.Id, Box = 1, SessionsUntilReview = 0 });
        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v2.Id, Box = 3, SessionsUntilReview = 2 });
        await db.SaveChangesAsync();

        var service = new ProgressService(db);
        var result = await service.GetListProgressAsync(user.Id, list.Id);

        result.Should().NotBeNull();
        result!.BoxDistribution.Box1.Should().Be(1);
        result.BoxDistribution.Box3.Should().Be(1);
    }

    [Fact]
    public async Task GetListProgress_ReturnsProblemVocabulary()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "test", Role = UserRole.User, IsInitialized = true };
        var lang1 = new Language { Code = "la", DisplayName = "Latein" };
        var lang2 = new Language { Code = "de", DisplayName = "Deutsch" };
        db.Users.Add(user);
        db.Languages.AddRange(lang1, lang2);
        await db.SaveChangesAsync();

        var list = new VocabularyList
        {
            Name = "Test", UserId = user.Id,
            SourceLanguageId = lang1.Id, TargetLanguageId = lang2.Id,
            CreatedAt = DateTime.UtcNow
        };
        var v1 = new Vocabulary { Term = "res", Translations = "[\"Sache\"]" };
        list.Vocabularies.Add(v1);
        db.VocabularyLists.Add(list);
        await db.SaveChangesAsync();

        db.BoxEntries.Add(new BoxEntry { UserId = user.Id, VocabularyId = v1.Id, Box = 1, SessionsUntilReview = 0 });

        var session = new TrainingSession
        {
            UserId = user.Id, ListId = list.Id,
            Mode = TrainingMode.SinglePass, StartedAt = DateTime.UtcNow
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync();

        // 3 wrong answers
        for (int i = 0; i < 3; i++)
            db.TrainingAnswers.Add(new TrainingAnswer
            {
                SessionId = session.Id, VocabularyId = v1.Id,
                Direction = Direction.SourceToTarget, GivenAnswer = "falsch",
                IsCorrect = false, AnsweredAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new ProgressService(db);
        var result = await service.GetListProgressAsync(user.Id, list.Id);

        result!.ProblemVocabulary.Should().HaveCount(1);
        result.ProblemVocabulary[0].Term.Should().Be("res");
        result.ProblemVocabulary[0].TimesWrong.Should().Be(3);
    }
}
```

- [ ] **Step 5: Implement ProgressService**

```csharp
// Services/ProgressService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Shared.Dtos.Lists;
using VokabelTrainer.Shared.Dtos.Progress;

public class ProgressService(AppDbContext db)
{
    public async Task<ListProgressDto?> GetListProgressAsync(int userId, int listId)
    {
        var list = await db.VocabularyLists
            .Where(l => l.Id == listId && l.UserId == userId)
            .FirstOrDefaultAsync();

        if (list is null) return null;

        var vocabIds = await db.Vocabularies
            .Where(v => v.ListId == listId)
            .Select(v => v.Id)
            .ToListAsync();

        var boxEntries = await db.BoxEntries
            .Where(b => b.UserId == userId && vocabIds.Contains(b.VocabularyId))
            .ToListAsync();

        var boxDist = new BoxDistributionDto(
            boxEntries.Count(b => b.Box == 1),
            boxEntries.Count(b => b.Box == 2),
            boxEntries.Count(b => b.Box == 3),
            boxEntries.Count(b => b.Box == 4),
            boxEntries.Count(b => b.Box == 5));

        var sessions = await db.TrainingSessions
            .Where(s => s.UserId == userId && s.ListId == listId && s.CompletedAt != null)
            .OrderBy(s => s.StartedAt)
            .Select(s => new SessionHistoryEntryDto(
                s.Id,
                s.StartedAt,
                s.TotalQuestions > 0 ? Math.Round((double)s.CorrectAnswers / s.TotalQuestions * 100, 1) : 0))
            .ToListAsync();

        var wrongCounts = await db.TrainingAnswers
            .Where(a => !a.IsCorrect && vocabIds.Contains(a.VocabularyId))
            .GroupBy(a => a.VocabularyId)
            .Select(g => new { VocabularyId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var problemVocab = new List<ProblemVocabularyDto>();
        foreach (var wc in wrongCounts)
        {
            var vocab = await db.Vocabularies.FindAsync(wc.VocabularyId);
            var box = boxEntries.FirstOrDefault(b => b.VocabularyId == wc.VocabularyId)?.Box ?? 1;
            if (vocab is not null)
                problemVocab.Add(new ProblemVocabularyDto(vocab.Term, wc.Count, box));
        }

        return new ListProgressDto(listId, list.Name, boxDist, sessions.Count, sessions, problemVocab);
    }
}
```

- [ ] **Step 6: Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add LanguageService, UserService, VocabularyListService, and ProgressService"
```

---

## Task 9: API Controllers and Program.cs Setup

**Files:**
- Create: `src/VokabelTrainer.Api/Controllers/AuthController.cs`
- Create: `src/VokabelTrainer.Api/Controllers/LanguagesController.cs`
- Create: `src/VokabelTrainer.Api/Controllers/UsersController.cs`
- Create: `src/VokabelTrainer.Api/Controllers/VocabularyListsController.cs`
- Create: `src/VokabelTrainer.Api/Controllers/TrainingController.cs`
- Create: `src/VokabelTrainer.Api/Controllers/ProgressController.cs`
- Modify: `src/VokabelTrainer.Api/Program.cs`

- [ ] **Step 1: Configure Program.cs**

```csharp
// Program.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/api/auth/unauthorized";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 403;
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

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
```

- [ ] **Step 2: Implement AuthController**

```csharp
// Controllers/AuthController.cs
namespace VokabelTrainer.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Shared.Dtos.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginOrSetupAsync(request.Username, request.Password);
        if (result is null)
            return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.Username),
            new(ClaimTypes.Role, result.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var username = User.Identity?.Name;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return Ok(new { username, role });
    }

    [HttpGet("needs-setup")]
    [AllowAnonymous]
    public async Task<ActionResult<bool>> NeedsSetup()
    {
        return Ok(await authService.NeedsInitialSetupAsync());
    }
}
```

- [ ] **Step 3: Implement remaining controllers**

Thin controllers — each method calls the service and returns the result. Follow the pattern:

```csharp
// Controllers/LanguagesController.cs — [Authorize("AdminOnly")] for CUD, [Authorize] for Read
// Controllers/UsersController.cs — [Authorize("AdminOnly")] for all
// Controllers/VocabularyListsController.cs — [Authorize], extract userId from claims
// Controllers/TrainingController.cs — [Authorize], endpoints: POST start, GET next-question/{sessionId}, POST submit-answer, POST abort/{sessionId}, GET result/{sessionId}
// Controllers/ProgressController.cs — [Authorize], GET list/{listId}
```

Each controller follows the same pattern:
- Inject the service via constructor
- Extract `UserId` from `User.Claims` for user-scoped operations
- Return appropriate status codes (404, 401, 200)

Add a helper extension to extract UserId from claims — add to controllers or as a base class:

```csharp
// In each controller, use a helper:
private int GetUserId() =>
    int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException());
```

Note: This requires storing the user ID in claims during login. Update AuthController login to include:
```csharp
new(ClaimTypes.NameIdentifier, userId.ToString())
```

And update `AuthService.LoginOrSetupAsync` to return the user ID (add to `AuthResponse` DTO).

- [ ] **Step 4: Verify build**

```bash
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add API controllers and Program.cs configuration"
```

---

## Task 10: Blazor Client Setup and Layout

**Files:**
- Modify: `src/VokabelTrainer.Client/Program.cs`
- Create: `src/VokabelTrainer.Client/Services/ApiClient.cs`
- Create: `src/VokabelTrainer.Client/Services/AuthStateProvider.cs`
- Create: `src/VokabelTrainer.Client/Layout/MainLayout.razor`
- Create: `src/VokabelTrainer.Client/Layout/NavBar.razor`
- Create: `src/VokabelTrainer.Client/Components/LanguageFlag.razor`
- Modify: `src/VokabelTrainer.Client/wwwroot/index.html`
- Modify: `src/VokabelTrainer.Client/_Imports.razor`

- [ ] **Step 1: Configure Program.cs with HttpClient and auth**

```csharp
// Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using VokabelTrainer.Client;
using VokabelTrainer.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddLocalization();

await builder.Build().RunAsync();
```

- [ ] **Step 2: Implement ApiClient**

Typed wrapper around HttpClient with methods for all API calls. All methods correspond to the controller endpoints. Example:

```csharp
// Services/ApiClient.cs
namespace VokabelTrainer.Client.Services;
using System.Net.Http.Json;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Dtos.Languages;
using VokabelTrainer.Shared.Dtos.Lists;
using VokabelTrainer.Shared.Dtos.Training;
using VokabelTrainer.Shared.Dtos.Progress;
using VokabelTrainer.Shared.Dtos.Users;

public class ApiClient(HttpClient http)
{
    // Auth
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    public async Task LogoutAsync() => await http.PostAsync("api/auth/logout", null);

    public async Task<bool> NeedsSetupAsync()
        => await http.GetFromJsonAsync<bool>("api/auth/needs-setup");

    // Languages
    public async Task<List<LanguageDto>> GetLanguagesAsync()
        => await http.GetFromJsonAsync<List<LanguageDto>>("api/languages") ?? [];

    public async Task<LanguageDto?> CreateLanguageAsync(CreateLanguageRequest request)
    {
        var response = await http.PostAsJsonAsync("api/languages", request);
        return await response.Content.ReadFromJsonAsync<LanguageDto>();
    }

    public async Task<LanguageDto?> UpdateLanguageAsync(int id, UpdateLanguageRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/languages/{id}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<LanguageDto>();
    }

    public async Task DeleteLanguageAsync(int id)
        => await http.DeleteAsync($"api/languages/{id}");

    // Users (admin)
    public async Task<List<UserDto>> GetUsersAsync()
        => await http.GetFromJsonAsync<List<UserDto>>("api/users") ?? [];

    public async Task CreateUserAsync(CreateUserRequest request)
        => await http.PostAsJsonAsync("api/users", request);

    public async Task ResetPasswordAsync(int userId)
        => await http.PostAsync($"api/users/{userId}/reset-password", null);

    public async Task DeleteUserAsync(int userId)
        => await http.DeleteAsync($"api/users/{userId}");

    // Vocabulary Lists
    public async Task<List<VocabularyListSummaryDto>> GetListsAsync()
        => await http.GetFromJsonAsync<List<VocabularyListSummaryDto>>("api/vocabularylists") ?? [];

    public async Task<VocabularyListDto?> GetListAsync(int id)
        => await http.GetFromJsonAsync<VocabularyListDto>($"api/vocabularylists/{id}");

    public async Task<int> CreateListAsync(CreateVocabularyListRequest request)
    {
        var response = await http.PostAsJsonAsync("api/vocabularylists", request);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<bool> UpdateListAsync(int id, UpdateVocabularyListRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/vocabularylists/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task DeleteListAsync(int id)
        => await http.DeleteAsync($"api/vocabularylists/{id}");

    // Training
    public async Task<int> StartSessionAsync(StartSessionRequest request)
    {
        var response = await http.PostAsJsonAsync("api/training/start", request);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<TrainingQuestionDto?> GetNextQuestionAsync(int sessionId)
        => await http.GetFromJsonAsync<TrainingQuestionDto?>($"api/training/next-question/{sessionId}");

    public async Task<SubmitAnswerResponse?> SubmitAnswerAsync(SubmitAnswerRequest request)
    {
        var response = await http.PostAsJsonAsync("api/training/submit-answer", request);
        return await response.Content.ReadFromJsonAsync<SubmitAnswerResponse>();
    }

    public async Task AbortSessionAsync(int sessionId)
        => await http.PostAsync($"api/training/abort/{sessionId}", null);

    public async Task<SessionResultDto?> GetSessionResultAsync(int sessionId)
        => await http.GetFromJsonAsync<SessionResultDto?>($"api/training/result/{sessionId}");

    // Progress
    public async Task<ListProgressDto?> GetListProgressAsync(int listId)
        => await http.GetFromJsonAsync<ListProgressDto?>($"api/progress/list/{listId}");
}
```

- [ ] **Step 3: Implement AuthStateProvider**

```csharp
// Services/AuthStateProvider.cs
namespace VokabelTrainer.Client.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using VokabelTrainer.Shared.Dtos.Auth;
using VokabelTrainer.Shared.Models;

public class AuthStateProvider : AuthenticationStateProvider
{
    private AuthResponse? _currentUser;

    public AuthResponse? CurrentUser => _currentUser;

    public void SetUser(AuthResponse? user)
    {
        _currentUser = user;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal principal;
        if (_currentUser is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, _currentUser.Username),
                new(ClaimTypes.Role, _currentUser.Role.ToString())
            };
            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }
        else
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity());
        }
        return Task.FromResult(new AuthenticationState(principal));
    }

    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;
}
```

- [ ] **Step 4: Add MudBlazor to index.html**

In `wwwroot/index.html`, add MudBlazor CSS und JS im `<head>` bzw. vor `</body>`:

```html
<!-- im <head> -->
<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

<!-- vor </body> -->
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
```

- [ ] **Step 5: Create MainLayout and NavBar**

`MainLayout.razor` — MudBlazor Layout mit `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`:

```razor
@inherits LayoutComponentBase

<MudThemeProvider />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu"
                       Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="@ToggleDrawer" />
        <MudText Typo="Typo.h6">Vokabel Trainer</MudText>
        <MudSpacer />
        <MudIconButton Icon="@Icons.Material.Filled.Logout"
                       Color="Color.Inherit" OnClick="@Logout" />
    </MudAppBar>
    <MudDrawer @bind-Open="_drawerOpen" Elevation="2">
        <NavBar />
    </MudDrawer>
    <MudMainContent Class="pa-4">
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen;
    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    [Inject] private ApiClient Api { get; set; } = default!;
    [Inject] private AuthStateProvider AuthState { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private async Task Logout()
    {
        await Api.LogoutAsync();
        AuthState.SetUser(null);
        Nav.NavigateTo("/login");
    }
}
```

`NavBar.razor` — MudBlazor Navigation:

```razor
<MudNavMenu>
    <MudNavLink Href="/" Icon="@Icons.Material.Filled.Dashboard">Dashboard</MudNavLink>
    <AuthorizeView Roles="Admin">
        <MudNavLink Href="/admin/users" Icon="@Icons.Material.Filled.People">Benutzer</MudNavLink>
        <MudNavLink Href="/admin/languages" Icon="@Icons.Material.Filled.Language">Sprachen</MudNavLink>
    </AuthorizeView>
</MudNavMenu>
```

- [ ] **Step 5: Create LanguageFlag component**

```razor
@* Components/LanguageFlag.razor *@
@if (!string.IsNullOrEmpty(FlagSvg))
{
    @((MarkupString)FlagSvg)
}

@code {
    [Parameter] public string? FlagSvg { get; set; }
}
```

- [ ] **Step 6: Minimales app.css fuer Overrides**

MudBlazor liefert das gesamte Styling. `app.css` nur fuer projektspezifische Overrides (z.B. Box-Verteilungs-Farben, LanguageFlag-Sizing). Kein eigenes Layout/Spacing/Typography noetig.

- [ ] **Step 7: Set up _Imports.razor, App.razor, Routes.razor**

`_Imports.razor` — add all required `@using` directives:
```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.WebAssembly.Http
@using Microsoft.Extensions.Localization
@using MudBlazor
@using VokabelTrainer.Client
@using VokabelTrainer.Client.Layout
@using VokabelTrainer.Client.Services
@using VokabelTrainer.Client.Components
@using VokabelTrainer.Shared.Models
@using VokabelTrainer.Shared.Dtos.Auth
@using VokabelTrainer.Shared.Dtos.Languages
@using VokabelTrainer.Shared.Dtos.Lists
@using VokabelTrainer.Shared.Dtos.Training
@using VokabelTrainer.Shared.Dtos.Progress
@using VokabelTrainer.Shared.Dtos.Users
```

`App.razor` — wrap in `CascadingAuthenticationState`:
```razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)">
                <NotAuthorized>
                    <RedirectToLogin />
                </NotAuthorized>
            </AuthorizeRouteView>
        </Found>
        <NotFound>
            <LayoutView Layout="@typeof(MainLayout)">
                <p>Seite nicht gefunden.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

- [ ] **Step 8: Set up localization**

```bash
cd src/VokabelTrainer.Client
dotnet add package Microsoft.Extensions.Localization
```

Add to `Program.cs`:
```csharp
builder.Services.AddLocalization();
```

Create `Resources/` directory. For each page, create a `.de.resx` file (e.g., `Resources/Pages/Login.de.resx`). Use `IStringLocalizer<PageName>` in each Razor page:
```razor
@inject IStringLocalizer<Login> L
<h2>@L["Anmelden"]</h2>
```

All UI-visible strings must go through `IStringLocalizer`. Start with German strings as default culture.

- [ ] **Step 8: Verify build**

```bash
dotnet build
```

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: set up Blazor client with layout, ApiClient, auth state, and localization"
```

---

## Task 11: Login Page

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/Login.razor`

- [ ] **Step 1: Implement Login.razor**

Page at route `/login`. Use `MudCard`, `MudTextField`, `MudButton`. Two states:
1. **Initial setup** (no users in DB) — shows "Admin erstellen" header, username + password + confirmation fields
2. **Normal login** — username + password fields
3. **First login for whitelisted user** (uninitialized) — username + password (sets password)

On load, call `ApiClient.NeedsSetupAsync()` to determine which mode.

On submit, call `ApiClient.LoginAsync()`. On success, call `AuthStateProvider.SetUser()` and navigate to Dashboard. Fehler mit `MudAlert` anzeigen.

- [ ] **Step 2: Verify build and manual test**

```bash
dotnet build
dotnet run --project src/VokabelTrainer.Api
```

Open in browser, verify login page renders.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add Login page with admin setup and password initialization"
```

---

## Task 12: Dashboard Page

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/Dashboard.razor`
- Create: `src/VokabelTrainer.Client/Components/BoxDistribution.razor`

- [ ] **Step 1: Implement BoxDistribution component**

Renders die farbige Box-Verteilung (Box 1-5 mit proportionalen Breiten). Accepts `BoxDistributionDto` as parameter. Kann einfach mit `<div>` und inline CSS oder mit `MudProgressLinear` Segmenten gebaut werden.

- [ ] **Step 2: Implement Dashboard.razor**

Page at route `/`. Requires auth. On load, fetch lists via `ApiClient.GetListsAsync()`.

MudBlazor-Komponenten:
- `MudText` Header mit `MudButton` "+ Neue Liste" und "Alle trainieren"
- Pro Liste: `MudCard` mit `MudCardContent` (Name, LanguageFlag, Vokabelanzahl, BoxDistribution), `MudCardActions` (MudButton: Trainieren, Bearbeiten, Fortschritt)
- `AuthorizeView Roles="Admin"` fuer Admin-Link

- [ ] **Step 3: Verify build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Dashboard page with list overview and box distribution"
```

---

## Task 13: Admin Pages

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/Admin/UserManagement.razor`
- Create: `src/VokabelTrainer.Client/Pages/Admin/LanguageManagement.razor`

- [ ] **Step 1: Implement UserManagement.razor**

Page at route `/admin/users`. Requires admin role. `MudTable` mit Benutzern, `MudIconButton` fuer Reset Password und Delete. `MudTextField` + `MudButton` zum Anlegen neuer Benutzer. Loeschen/Reset mit `MudDialog` bestaetigen.

- [ ] **Step 2: Implement LanguageManagement.razor**

Page at route `/admin/languages`. Requires admin role. `MudTable` mit Sprachen (Code, Anzeigename, Flag-Preview). `MudDialog` zum Erstellen/Bearbeiten (MudTextField fuer Code, Name; MudTextField Multiline fuer SVG).

- [ ] **Step 3: Verify build**

```bash
dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add admin pages for user and language management"
```

---

## Task 14: List Editor Page

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/ListEditor.razor`

- [ ] **Step 1: Implement ListEditor.razor**

Page at route `/lists/new` and `/lists/{id}/edit`. On load:
- Fetch languages via `ApiClient.GetLanguagesAsync()` for dropdowns
- If editing, fetch list via `ApiClient.GetListAsync(id)` and populate form
- For editing, reconstruct raw text from entries (`Term = Translation1, Translation2`)

MudBlazor-Komponenten:
- `MudTextField` fuer Name
- `MudSelect<int>` fuer Source/Target Language (mit LanguageFlag im Item-Template)
- `MudTextField` Lines="10" fuer Raw Vocabulary (eine Vokabel pro Zeile: `Begriff = Uebersetzung1, Uebersetzung2`)
- `MudButton` Speichern, `MudButton` Abbrechen

On save: call Create or Update API. Erfolg/Fehler mit `MudSnackbar` anzeigen.

- [ ] **Step 2: Verify build**

```bash
dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: add vocabulary list editor page"
```

---

## Task 15: Training Flow Pages

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/TrainingStart.razor`
- Create: `src/VokabelTrainer.Client/Pages/Training.razor`
- Create: `src/VokabelTrainer.Client/Pages/SessionResult.razor`

- [ ] **Step 1: Implement TrainingStart.razor**

Page at route `/training/start/{listId?}`. MudBlazor-Komponenten:
- `MudRadioGroup` fuer Moduswahl: Einmal durch / Endlos
- `MudNumericField` optional fuer max Vokabelanzahl
- `MudButton` Start

On start: call `ApiClient.StartSessionAsync()`, navigate to `/training/{sessionId}`.

- [ ] **Step 2: Implement Training.razor**

Page at route `/training/{sessionId}`. Core training loop:
- On load and after each answer: call `ApiClient.GetNextQuestionAsync(sessionId)`
- `MudCard` mit Richtungsanzeige (LanguageFlag Komponenten), `MudText` Typo.h4 fuer Prompt-Wort
- `MudProgressLinear` fuer Fortschritt (x/n)
- `MudTextField` fuer Antwort (AutoFocus, submit on Enter via `OnKeyDown`)
- `MudButton` "Pruefen"
- Nach Submit: `MudAlert` Severity.Success/Error mit Feedback, bei falsch die korrekten Antworten
- `MudButton` "Weiter" fuer naechste Frage
- `MudButton` "Abbrechen" (Endlos-Modus) ruft `AbortSessionAsync`
- When `SessionComplete` is true, navigate to `/training/result/{sessionId}`

- [ ] **Step 3: Implement SessionResult.razor**

Page at route `/training/result/{sessionId}`. On load, call `ApiClient.GetSessionResultAsync()`.

MudBlazor-Komponenten:
- `MudText` Typo.h2 fuer Prozent-Anzeige, `MudText` fuer "x von y richtig"
- `MudSimpleTable` fuer falsch beantwortete Vokabeln (Term, korrekte Antworten, gegebene Antwort)
- `MudButton` "Nochmal trainieren" (navigiert zu TrainingStart mit gleicher listId)
- `MudButton` "Zurueck" (navigiert zu Dashboard)

- [ ] **Step 4: Verify build**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add training flow pages (start, quiz, result)"
```

---

## Task 16: Progress Page with Charts

**Files:**
- Create: `src/VokabelTrainer.Client/Pages/Progress.razor`
- Create: `src/VokabelTrainer.Client/Components/LeitnerExplanation.razor`

- [ ] **Step 1: Charts mit MudBlazor**

MudBlazor hat eingebaute Chart-Komponenten (`MudChart`). Kein Chart.js oder JS-Interop noetig.

Box-Verteilung als `MudChart ChartType="ChartType.Bar"`:
```razor
<MudChart ChartType="ChartType.Bar"
          ChartSeries="@_boxSeries"
          XAxisLabels="@(new[] { "Box 1", "Box 2", "Box 3", "Box 4", "Box 5" })"
          Width="100%" Height="200px" />
```

Erfolgsquote ueber Zeit als `MudChart ChartType="ChartType.Line"`:
```razor
<MudChart ChartType="ChartType.Line"
          ChartSeries="@_successSeries"
          XAxisLabels="@_sessionLabels"
          Width="100%" Height="200px" />
```

- [ ] **Step 2: Implement LeitnerExplanation component**

Static component explaining the Leitner box system with colored indicators. Same text as in the wireframes.

- [ ] **Step 3: Implement Progress.razor**

Page at route `/progress/{listId}`. On load, call `ApiClient.GetListProgressAsync(listId)`.

MudBlazor-Komponenten:
- `MudText` Listenname mit LanguageFlag
- LeitnerExplanation Component
- `MudChart` ChartType.Bar fuer Box-Verteilung (5 Boxen, farbig)
- `MudChart` ChartType.Line fuer Erfolgsquote ueber Sessions
- `MudText` Anzahl absolvierte Sessions
- `MudSimpleTable` fuer Problemvokabeln (Term, x-mal falsch, aktuelle Box)

- [ ] **Step 4: Verify build**

```bash
dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add progress page with Chart.js visualizations and Leitner explanation"
```

---

## Task 17: PWA Configuration and Final Polish

**Files:**
- Modify: `src/VokabelTrainer.Client/wwwroot/manifest.json`
- Modify: `src/VokabelTrainer.Client/wwwroot/service-worker.js`
- Modify: `src/VokabelTrainer.Client/wwwroot/css/app.css`

- [ ] **Step 1: Configure PWA manifest**

```json
{
  "name": "Vokabel Trainer",
  "short_name": "Vokabeln",
  "start_url": "/",
  "display": "standalone",
  "background_color": "#1a1a2e",
  "theme_color": "#5b6abf",
  "icons": [
    { "src": "icon-192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "icon-512.png", "sizes": "512x512", "type": "image/png" }
  ]
}
```

- [ ] **Step 2: Create PWA icons**

Create simple SVG-based icons at `wwwroot/icon-192.png` and `wwwroot/icon-512.png`. Can be a simple "V" letter on a colored background. Use a tool like ImageMagick or create a minimal SVG and convert, or provide placeholder PNGs.

- [ ] **Step 3: Service worker — cache-first for static assets only**

No offline data caching (out of scope per spec). Just cache the WASM runtime and static assets for fast subsequent loads.

- [ ] **Step 4: Final polish**

MudBlazor liefert bereits mobile-friendly Touch Targets. Pruefen ob alle Seiten auf 360px Breite korrekt rendern. Ggf. `MudContainer MaxWidth="MaxWidth.Small"` auf Seiten die zu breit werden.

- [ ] **Step 4: Add .superpowers/ to .gitignore**

- [ ] **Step 5: Full build and manual smoke test**

```bash
dotnet build
dotnet run --project src/VokabelTrainer.Api
```

Test flow:
1. Open in mobile-width browser
2. First login → becomes admin
3. Admin: create languages (Latein, Deutsch, Englisch)
4. Admin: create a user
5. Logout, login as user
6. Create vocabulary list
7. Start training (both modes)
8. Check progress page

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: configure PWA, finalize CSS, and polish mobile experience"
```

---

## Task Summary

| Task | Description | Dependencies |
|------|-------------|-------------|
| 1 | Solution scaffolding | None |
| 2 | Shared enums and DTOs | 1 |
| 3 | EF Core data model and DbContext | 1, 2 |
| 4 | VocabularyParser (TDD) | 1 |
| 5 | AuthService (TDD) | 3 |
| 6 | LeitnerService (TDD) | 3 |
| 7 | TrainingService (TDD) | 3, 4, 6 |
| 8 | Remaining backend services | 3, 4, 6 |
| 9 | API controllers and Program.cs | 5, 7, 8 |
| 10 | Blazor client setup and layout | 1, 2 |
| 11 | Login page | 9, 10 |
| 12 | Dashboard page | 9, 10 |
| 13 | Admin pages | 9, 10 |
| 14 | List editor page | 9, 10 |
| 15 | Training flow pages | 9, 10 |
| 16 | Progress page with charts | 9, 10 |
| 17 | PWA and final polish | 11-16 |

**Parallelizable:** Tasks 4+5+6 can run in parallel. Tasks 11-16 can run in parallel (all depend on 9+10). Tasks 2+4 can start immediately after 1.
