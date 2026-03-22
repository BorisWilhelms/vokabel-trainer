# Parent Access & Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add parent accounts with read-only access to children's learning progress, plus an invitation-based registration system.

**Architecture:** New `Role` enum (Child/Parent) + `IsAdmin` bool on User entity. `ParentChild` join table for n:m relationships. `Invitation` entity for token-based registration. Existing `UserRole` enum and `IsInitialized` field removed. Authorization via `Policies.ChildOnly`/`Policies.ParentOnly` constants. Parent endpoints reuse `ProgressService` to display children's data.

**Tech Stack:** .NET 10, EF Core + SQLite, Minimal API, Razor Components, Bulma CSS, HTMX

**Spec:** `docs/superpowers/specs/2026-03-22-parent-access-design.md`

---

## File Structure

### New Files

| File | Purpose |
|------|---------|
| `src/.../Models/Role.cs` | `Role` enum (Child, Parent) |
| `src/.../Models/Policies.cs` | Policy name constants |
| `src/.../Data/Entities/ParentChild.cs` | Join table entity |
| `src/.../Data/Entities/Invitation.cs` | Invitation entity |
| `src/.../Services/InvitationService.cs` | Invitation CRUD + token generation |
| `src/.../Services/RegistrationService.cs` | User registration logic (create user, link, redeem) |
| `src/.../Models/Parent/ChildSummaryDto.cs` | DTO for parent dashboard child list |
| `src/.../Services/ParentService.cs` | Parent dashboard data (children list, progress proxy) |
| `src/.../Endpoints/RegistrationEndpoints.cs` | `GET/POST /register` |
| `src/.../Endpoints/ParentEndpoints.cs` | `GET /parent`, `GET /parent/child/{id}`, etc. |
| `src/.../Components/Pages/Register.razor` | Registration form |
| `src/.../Components/Pages/ParentDashboard.razor` | Parent overview (children list) |
| `src/.../Components/Pages/ParentChildProgress.razor` | Child progress detail (reuses Progress layout) |
| `tests/.../Services/InvitationServiceTests.cs` | Invitation service tests |
| `tests/.../Services/ParentServiceTests.cs` | Parent service tests |
| `tests/.../Services/RegistrationServiceTests.cs` | Registration logic tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/.../Data/Entities/User.cs` | Replace `UserRole Role` → `Role Role` + `bool IsAdmin`, remove `IsInitialized` |
| `src/.../Data/AppDbContext.cs` | Add DbSets for ParentChild/Invitation, configure relationships |
| `src/.../Models/UserRole.cs` | **Deleted** |
| `src/.../Models/Auth/AuthResponse.cs` | Use `Role` + `bool IsAdmin` |
| `src/.../Models/Users/UserDto.cs` | Use `Role` + `bool IsAdmin`, remove `IsInitialized` |
| `src/.../Models/Users/CreateUserRequest.cs` | **Deleted** (replaced by invitation flow) |
| `src/.../Endpoints/HttpContextExtensions.cs` | Add `GetRole()` helper |
| `src/.../Endpoints/AuthEndpoints.cs` | New claim emission, role-based redirect, remove IsInitialized path |
| `src/.../Endpoints/AdminEndpoints.cs` | Replace user creation with invitations, add ParentChild management |
| `src/.../Endpoints/DashboardEndpoints.cs` | Add `Policies.ChildOnly` |
| `src/.../Endpoints/ListEndpoints.cs` | Add `Policies.ChildOnly` |
| `src/.../Endpoints/TrainingEndpoints.cs` | Add `Policies.ChildOnly` |
| `src/.../Endpoints/ProgressEndpoints.cs` | Add `Policies.ChildOnly` |
| `src/.../Services/AuthService.cs` | Adapt first-user bootstrap, remove `IsInitialized` logic |
| `src/.../Services/UserService.cs` | Remove `CreateAsync`, `ResetPasswordAsync`; add `UpdateRoleAsync`, `UpdateIsAdminAsync` |
| `src/.../Components/PageLayout.razor` | Add `Role` parameter, role-based navbar |
| `src/.../Components/Pages/Dashboard.razor` | Add `Role` parameter, "Eltern einladen" button |
| `src/.../Components/Pages/Admin/UserManagement.razor` | Rewrite: invitation creation, role editing, ParentChild management |
| `src/.../Components/Pages/Help.razor` | Add parent/invitation sections |
| `src/.../Program.cs` | Register new services, add policies, register new endpoints |
| `tests/.../Services/AuthServiceTests.cs` | Update for new Role enum, remove IsInitialized tests |

---

### Task 1: Data Model — Role enum, Policies, Entities

**Files:**
- Create: `src/VokabelTrainer.Api/Models/Role.cs`
- Create: `src/VokabelTrainer.Api/Models/Policies.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/ParentChild.cs`
- Create: `src/VokabelTrainer.Api/Data/Entities/Invitation.cs`
- Modify: `src/VokabelTrainer.Api/Data/Entities/User.cs`
- Modify: `src/VokabelTrainer.Api/Data/AppDbContext.cs`
- Delete: `src/VokabelTrainer.Api/Models/UserRole.cs`

- [ ] **Step 1: Create Role enum**

```csharp
// src/VokabelTrainer.Api/Models/Role.cs
namespace VokabelTrainer.Api.Models;

public enum Role { Child, Parent }
```

- [ ] **Step 2: Create Policies constants**

```csharp
// src/VokabelTrainer.Api/Models/Policies.cs
namespace VokabelTrainer.Api.Models;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ChildOnly = nameof(ChildOnly);
    public const string ParentOnly = nameof(ParentOnly);
}
```

- [ ] **Step 3: Create ParentChild entity**

```csharp
// src/VokabelTrainer.Api/Data/Entities/ParentChild.cs
namespace VokabelTrainer.Api.Data.Entities;

public class ParentChild
{
    public int ParentId { get; set; }
    public User Parent { get; set; } = null!;
    public int ChildId { get; set; }
    public User Child { get; set; } = null!;
}
```

- [ ] **Step 4: Create Invitation entity**

```csharp
// src/VokabelTrainer.Api/Data/Entities/Invitation.cs
using VokabelTrainer.Api.Models;

namespace VokabelTrainer.Api.Data.Entities;

public class Invitation
{
    public int Id { get; set; }
    public required string Token { get; set; }
    public Role Role { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public bool LinkToCreator { get; set; }
    public int UsesRemaining { get; set; } = 2;
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 5: Update User entity**

Replace `UserRole Role` with `Role Role` and `bool IsAdmin`. Remove `IsInitialized`.

```csharp
// src/VokabelTrainer.Api/Data/Entities/User.cs
namespace VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public string? PasswordHash { get; set; }
    public Role Role { get; set; }
    public bool IsAdmin { get; set; }
    public List<VocabularyList> VocabularyLists { get; set; } = [];
    public List<BoxEntry> BoxEntries { get; set; } = [];
    public List<TrainingSession> TrainingSessions { get; set; } = [];
}
```

- [ ] **Step 6: Delete UserRole enum**

Delete the file `src/VokabelTrainer.Api/Models/UserRole.cs`.

- [ ] **Step 7: Update AppDbContext**

Add DbSets and configure relationships for ParentChild and Invitation:

```csharp
// Add to AppDbContext class:
public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
public DbSet<Invitation> Invitations => Set<Invitation>();

// Add to OnModelCreating:
modelBuilder.Entity<ParentChild>()
    .HasKey(pc => new { pc.ParentId, pc.ChildId });

modelBuilder.Entity<ParentChild>()
    .HasOne(pc => pc.Parent)
    .WithMany()
    .HasForeignKey(pc => pc.ParentId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<ParentChild>()
    .HasOne(pc => pc.Child)
    .WithMany()
    .HasForeignKey(pc => pc.ChildId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<Invitation>()
    .HasIndex(i => i.Token).IsUnique();

modelBuilder.Entity<Invitation>()
    .HasOne(i => i.CreatedByUser)
    .WithMany()
    .HasForeignKey(i => i.CreatedByUserId)
    .OnDelete(DeleteBehavior.SetNull);
```

- [ ] **Step 8: Create EF migration**

Run: `dotnet ef migrations add AddParentAccessAndInvitations --project src/VokabelTrainer.Api`

This migration must handle:
- Rename `Role` column values: `0` (User) → `0` (Child), `1` (Admin) → `0` (Child)
- Add `IsAdmin` column: set to `true` where old `Role` was `1` (Admin), else `false`
- Remove `IsInitialized` column
- Create `ParentChildren` and `Invitations` tables

**Important:** SQLite doesn't support ALTER COLUMN. EF Core will generate a table rebuild (create temp table, copy data, drop old, rename). You must hand-edit the `Up()` method to ensure data is migrated correctly:

1. Before the table rebuild, add `IsAdmin` column with default `false` and populate it:
   ```sql
   ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0;
   UPDATE Users SET IsAdmin = 1 WHERE Role = 1;
   UPDATE Users SET Role = 0;
   ```
2. Then let the table rebuild proceed (it will copy the now-correct values).
3. The `IsInitialized` column is dropped as part of the rebuild.

Review the generated migration carefully — the exact SQL depends on what EF generates. The key invariant: `IsAdmin = true` where old `Role = 1` (Admin), and all `Role` values become `0` (Child).

- [ ] **Step 9: Verify migration applies cleanly**

Run: `dotnet build`
Expected: Build succeeds (compilation errors from UserRole references are expected at this point — they're fixed in subsequent tasks).

- [ ] **Step 10: Commit**

```
feat: add Role enum, Policies, ParentChild, Invitation entities, migration
```

---

### Task 2: DTOs, AuthResponse, HttpContextExtensions

**Files:**
- Modify: `src/VokabelTrainer.Api/Models/Auth/AuthResponse.cs`
- Modify: `src/VokabelTrainer.Api/Models/Users/UserDto.cs`
- Delete: `src/VokabelTrainer.Api/Models/Users/CreateUserRequest.cs`
- Delete: `src/VokabelTrainer.Api/Models/Auth/SetPasswordRequest.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/HttpContextExtensions.cs`

- [ ] **Step 1: Update AuthResponse**

```csharp
// src/VokabelTrainer.Api/Models/Auth/AuthResponse.cs
namespace VokabelTrainer.Api.Models.Auth;

public record AuthResponse(int UserId, string Username, Role Role, bool IsAdmin);
```

- [ ] **Step 2: Update UserDto**

```csharp
// src/VokabelTrainer.Api/Models/Users/UserDto.cs
namespace VokabelTrainer.Api.Models.Users;

public record UserDto(int Id, string Username, Role Role, bool IsAdmin);
```

- [ ] **Step 3: Delete CreateUserRequest and SetPasswordRequest**

Delete `src/VokabelTrainer.Api/Models/Users/CreateUserRequest.cs` and `src/VokabelTrainer.Api/Models/Auth/SetPasswordRequest.cs` (no longer needed).

- [ ] **Step 4: Add GetRole() helper to HttpContextExtensions**

```csharp
// src/VokabelTrainer.Api/Endpoints/HttpContextExtensions.cs
using System.Security.Claims;
using VokabelTrainer.Api.Models;

namespace VokabelTrainer.Api.Endpoints;

public static class HttpContextExtensions
{
    public static int GetUserId(this HttpContext ctx) =>
        int.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    public static Role GetRole(this HttpContext ctx) =>
        Enum.Parse<Role>(ctx.User.FindFirst("Role")!.Value);
}
```

- [ ] **Step 5: Commit**

```
feat: update DTOs for new Role model, add GetRole() helper
```

---

### Task 3: AuthService + AuthEndpoints Rewrite

**Files:**
- Modify: `src/VokabelTrainer.Api/Services/AuthService.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Program.cs`
- Modify: `tests/VokabelTrainer.Api.Tests/Services/AuthServiceTests.cs`

- [ ] **Step 1: Rewrite AuthService**

Remove `IsInitialized` logic. Adapt first-user bootstrap to create `Role = Child, IsAdmin = true`. Remove the uninitialized-user password-setup code path. `LoginOrSetupAsync` becomes simpler — only first-user-setup or regular login:

```csharp
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Models.Auth;

public class AuthService(AppDbContext db)
{
    public async Task<User?> GetByIdAsync(int id)
    {
        return await db.Users.FindAsync(id);
    }

    public async Task<bool> NeedsInitialSetupAsync()
    {
        return !await db.Users.AnyAsync();
    }

    public async Task<AuthResponse?> LoginOrSetupAsync(string username, string password)
    {
        if (!await db.Users.AnyAsync())
        {
            var admin = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = Role.Child,
                IsAdmin = true
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            return new AuthResponse(admin.Id, admin.Username, admin.Role, admin.IsAdmin);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || user.PasswordHash is null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        return new AuthResponse(user.Id, user.Username, user.Role, user.IsAdmin);
    }
}
```

- [ ] **Step 2: Rewrite AuthEndpoints claim emission + redirect**

Claims now emit `"Role"` custom claim for Child/Parent and `ClaimTypes.Role = "Admin"` only when `IsAdmin`. Post-login redirect depends on role. `OnValidatePrincipal` rebuilds claims:

```csharp
// In AuthEndpoints.MapAuthEndpoints:

// POST /login — update claim emission:
var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
    new(ClaimTypes.Name, result.Username),
    new("Role", result.Role.ToString())
};
if (result.IsAdmin)
    claims.Add(new(ClaimTypes.Role, "Admin"));

// ... SignInAsync ...

var redirectUrl = result.Role == Role.Parent ? "/parent" : "/";
return Results.Redirect(redirectUrl);
```

- [ ] **Step 3: Update OnValidatePrincipal in Program.cs to rebuild claims**

Replace the existing `OnValidatePrincipal` lambda. Instead of just checking user existence, rebuild the full `ClaimsPrincipal`:

```csharp
options.Events.OnValidatePrincipal = async context =>
{
    var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userId is null)
    {
        context.RejectPrincipal();
        return;
    }
    var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
    var user = await authService.GetByIdAsync(int.Parse(userId));
    if (user is null)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return;
    }
    // Rebuild claims from current DB state
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Username),
        new("Role", user.Role.ToString())
    };
    if (user.IsAdmin)
        claims.Add(new(ClaimTypes.Role, "Admin"));

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    context.ReplacePrincipal(new ClaimsPrincipal(identity));
    context.ShouldRenew = true;
};
```

- [ ] **Step 4: Register policies in Program.cs**

Replace existing `AddAuthorizationBuilder` call:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole("Admin"))
    .AddPolicy(Policies.ChildOnly, policy => policy.RequireClaim("Role", "Child"))
    .AddPolicy(Policies.ParentOnly, policy => policy.RequireClaim("Role", "Parent"));
```

Add `using VokabelTrainer.Api.Models;` to Program.cs.

- [ ] **Step 5: Update AuthService tests**

Replace `UserRole.Admin` → `Role.Child` + `IsAdmin = true`, `UserRole.User` → `Role.Child`. Remove `IsInitialized` references and the `Login_UninitializedUser_SetsPassword` test. Update assertions for new `AuthResponse` shape.

- [ ] **Step 6: Run tests**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```
feat: rewrite auth for Role enum, claims rebuild, policy registration
```

---

### Task 4: UserService + AdminEndpoints Update

**Files:**
- Modify: `src/VokabelTrainer.Api/Services/UserService.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/AdminEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Components/Pages/Admin/UserManagement.razor`

- [ ] **Step 1: Rewrite UserService**

Remove `CreateAsync` and `ResetPasswordAsync`. Add `UpdateRoleAsync` and `UpdateIsAdminAsync`:

```csharp
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Models.Users;

public class UserService(AppDbContext db)
{
    public async Task<List<UserDto>> GetAllAsync()
        => await db.Users
            .Select(u => new UserDto(u.Id, u.Username, u.Role, u.IsAdmin))
            .ToListAsync();

    public async Task<bool> UpdateRoleAsync(int id, Role role)
    {
        var user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return false;
        user.Role = role;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateIsAdminAsync(int id, bool isAdmin)
    {
        var user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return false;
        user.IsAdmin = isAdmin;
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

- [ ] **Step 2: Update AdminEndpoints for user management**

Remove `POST /admin/users` (create user) and `POST /admin/users/{id}/reset` (reset password). Add endpoints for role/admin editing. Replace `"AdminOnly"` strings with `Policies.AdminOnly`:

- `POST /admin/users/{id}/role` — update role
- `POST /admin/users/{id}/admin` — toggle IsAdmin
- Keep `POST /admin/users/{id}/delete`
- All language endpoints: replace `"AdminOnly"` → `Policies.AdminOnly`

- [ ] **Step 3: Rewrite UserManagement.razor**

Remove "Neuen Benutzer anlegen" form and "Status" column (IsInitialized). Replace with:
- Table columns: Benutzername, Rolle (Kind/Elternteil), Admin, Aktionen
- Per user: dropdown to change Role, checkbox/button to toggle IsAdmin, delete button
- Link to invitation management and ParentChild management sections (handled in Task 7)

- [ ] **Step 4: Run tests**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```
feat: update user management — role editing, remove legacy user creation
```

---

### Task 5: InvitationService + Tests

**Files:**
- Create: `src/VokabelTrainer.Api/Services/InvitationService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/InvitationServiceTests.cs`
- Modify: `src/VokabelTrainer.Api/Program.cs` (register service)

- [ ] **Step 1: Write InvitationService tests**

```csharp
// tests/VokabelTrainer.Api.Tests/Services/InvitationServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;

public class InvitationServiceTests
{
    [Fact]
    public async Task CreateInvitation_GeneratesUniqueToken()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new InvitationService(db);

        var inv = await service.CreateAsync(user.Id, Role.Parent, linkToCreator: true);

        inv.Token.Should().HaveLength(32);
        inv.Role.Should().Be(Role.Parent);
        inv.UsesRemaining.Should().Be(2);
        inv.LinkToCreator.Should().BeTrue();
    }

    [Fact]
    public async Task GetByToken_ValidToken_ReturnsInvitation()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new InvitationService(db);
        var inv = await service.CreateAsync(user.Id, Role.Parent, linkToCreator: true);

        var found = await service.GetByTokenAsync(inv.Token);
        found.Should().NotBeNull();
        found!.Id.Should().Be(inv.Id);
    }

    [Fact]
    public async Task GetByToken_ExhaustedToken_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new InvitationService(db);
        var inv = await service.CreateAsync(user.Id, Role.Parent, linkToCreator: true);

        // Exhaust uses
        var entity = await db.Invitations.FindAsync(inv.Id);
        entity!.UsesRemaining = 0;
        db.Invitations.Update(entity);
        await db.SaveChangesAsync();

        var found = await service.GetByTokenAsync(inv.Token);
        found.Should().BeNull();
    }

    [Fact]
    public async Task RedeemInvitation_DecrementsUses()
    {
        using var db = TestDbContextFactory.Create();
        var user = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var service = new InvitationService(db);
        var inv = await service.CreateAsync(user.Id, Role.Parent, linkToCreator: true);

        await service.RedeemAsync(inv.Id);

        var updated = await db.Invitations.FindAsync(inv.Id);
        updated!.UsesRemaining.Should().Be(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter InvitationServiceTests`
Expected: Compilation error (InvitationService doesn't exist yet).

- [ ] **Step 3: Implement InvitationService**

```csharp
// src/VokabelTrainer.Api/Services/InvitationService.cs
namespace VokabelTrainer.Api.Services;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;

public class InvitationService(AppDbContext db)
{
    public async Task<Invitation> CreateAsync(int createdByUserId, Role role, bool linkToCreator)
    {
        var invitation = new Invitation
        {
            Token = GenerateToken(),
            Role = role,
            CreatedByUserId = createdByUserId,
            LinkToCreator = linkToCreator,
            UsesRemaining = 2,
            CreatedAt = DateTime.UtcNow
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
        return invitation;
    }

    public async Task<Invitation?> GetByTokenAsync(string token)
    {
        return await db.Invitations
            .Include(i => i.CreatedByUser)
            .FirstOrDefaultAsync(i => i.Token == token && i.UsesRemaining > 0);
    }

    public async Task RedeemAsync(int invitationId)
    {
        var invitation = await db.Invitations.AsTracking().FirstAsync(i => i.Id == invitationId);
        invitation.UsesRemaining--;
        await db.SaveChangesAsync();
    }

    public async Task<List<Invitation>> GetAllAsync()
    {
        return await db.Invitations
            .Include(i => i.CreatedByUser)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var invitation = await db.Invitations.FindAsync(id);
        if (invitation is null) return false;
        db.Invitations.Remove(invitation);
        await db.SaveChangesAsync();
        return true;
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Register InvitationService in Program.cs**

Add: `builder.Services.AddScoped<InvitationService>();`

- [ ] **Step 5: Run tests**

Run: `dotnet test --filter InvitationServiceTests`
Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```
feat: add InvitationService with token generation and CRUD
```

---

### Task 6: Registration Flow

**Files:**
- Create: `src/VokabelTrainer.Api/Endpoints/RegistrationEndpoints.cs`
- Create: `src/VokabelTrainer.Api/Components/Pages/Register.razor`
- Modify: `src/VokabelTrainer.Api/Program.cs` (map endpoints)

- [ ] **Step 1: Create RegistrationEndpoints**

```csharp
// src/VokabelTrainer.Api/Endpoints/RegistrationEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class RegistrationEndpoints
{
    public static WebApplication MapRegistrationEndpoints(this WebApplication app)
    {
        app.MapGet("/register", async (string? invite, InvitationService invitationService) =>
        {
            if (string.IsNullOrWhiteSpace(invite))
                return Results.Redirect("/login");

            var invitation = await invitationService.GetByTokenAsync(invite);

            return new RazorComponentResult<Register>(new
            {
                Token = invite,
                IsValid = invitation is not null,
                RoleName = invitation?.Role == Role.Parent ? "Elternteil" : "Kind",
                ErrorMessage = (string?)null
            });
        });

        app.MapPost("/register", async (HttpContext ctx, InvitationService invitationService, AppDbContext db) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var token = form["Token"].FirstOrDefault() ?? "";
            var username = form["Username"].FirstOrDefault() ?? "";
            var password = form["Password"].FirstOrDefault() ?? "";
            var passwordConfirmation = form["PasswordConfirmation"].FirstOrDefault() ?? "";

            var invitation = await invitationService.GetByTokenAsync(token);
            if (invitation is null)
                return Results.Redirect("/login");

            string? error = null;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                error = "credentials";
            else if (password != passwordConfirmation)
                error = "mismatch";
            else if (await db.Users.AnyAsync(u => u.Username == username))
                error = "exists";

            if (error is not null)
            {
                return new RazorComponentResult<Register>(new
                {
                    Token = token,
                    IsValid = true,
                    RoleName = invitation.Role == Role.Parent ? "Elternteil" : "Kind",
                    ErrorMessage = error switch
                    {
                        "credentials" => "Benutzername und Passwort sind erforderlich.",
                        "mismatch" => "Die Passwörter stimmen nicht überein.",
                        "exists" => "Dieser Benutzername ist bereits vergeben.",
                        _ => null
                    }
                });
            }

            // Create user
            var user = new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = invitation.Role,
                IsAdmin = false
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Link to creator if applicable
            if (invitation.LinkToCreator && invitation.CreatedByUserId.HasValue)
            {
                var link = invitation.Role == Role.Parent
                    ? new ParentChild { ParentId = user.Id, ChildId = invitation.CreatedByUserId.Value }
                    : new ParentChild { ParentId = invitation.CreatedByUserId.Value, ChildId = user.Id };
                db.ParentChildren.Add(link);
                await db.SaveChangesAsync();
            }

            // Redeem invitation
            await invitationService.RedeemAsync(invitation.Id);

            // Sign in
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new("Role", user.Role.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            var redirectUrl = user.Role == Role.Parent ? "/parent" : "/";
            return Results.Redirect(redirectUrl);
        }).DisableAntiforgery();

        return app;
    }
}
```

- [ ] **Step 2: Create Register.razor**

```razor
@* src/VokabelTrainer.Api/Components/Pages/Register.razor *@
<PageLayout ShowNavbar="false">
    <h3 class="title has-text-centered">Registrieren als @RoleName</h3>

    @if (!IsValid)
    {
        <div class="notification is-danger">
            Dieser Einladungslink ist ungültig oder wurde bereits verwendet.
        </div>
        <a href="/login" class="button is-primary">Zur Anmeldung</a>
    }
    else
    {
        @if (ErrorMessage is not null)
        {
            <div class="notification is-danger">@ErrorMessage</div>
        }

        <form method="post" action="/register">
            <input type="hidden" name="Token" value="@Token" />

            <div class="field">
                <label class="label" for="username">Benutzername</label>
                <div class="control">
                    <input id="username" class="input" type="text" name="Username" required />
                </div>
            </div>

            <div class="field">
                <label class="label" for="password">Passwort</label>
                <div class="control">
                    <input id="password" type="password" class="input" name="Password" required />
                </div>
            </div>

            <div class="field">
                <label class="label" for="password-confirm">Passwort wiederholen</label>
                <div class="control">
                    <input id="password-confirm" type="password" class="input" name="PasswordConfirmation" required />
                </div>
            </div>

            <div class="field">
                <button type="submit" class="button is-primary is-fullwidth">Registrieren</button>
            </div>
        </form>
    }
</PageLayout>

@code {
    [Parameter] public string Token { get; set; } = "";
    [Parameter] public bool IsValid { get; set; }
    [Parameter] public string RoleName { get; set; } = "";
    [Parameter] public string? ErrorMessage { get; set; }
}
```

- [ ] **Step 3: Register endpoints in Program.cs**

Add: `app.MapRegistrationEndpoints();` after the other `Map*Endpoints` calls.

- [ ] **Step 4: Extract registration logic into a testable RegistrationService**

The `POST /register` endpoint has non-trivial logic (user creation, ParentChild linking direction, invitation redemption). Extract this into a `RegistrationService` with a `RegisterAsync` method so it can be unit tested. The endpoint becomes a thin wrapper.

```csharp
// src/VokabelTrainer.Api/Services/RegistrationService.cs
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;

public class RegistrationService(AppDbContext db, InvitationService invitationService)
{
    public async Task<User?> RegisterAsync(Invitation invitation, string username, string password)
    {
        if (await db.Users.AnyAsync(u => u.Username == username))
            return null;

        var user = new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = invitation.Role,
            IsAdmin = false
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        if (invitation.LinkToCreator && invitation.CreatedByUserId.HasValue)
        {
            var link = invitation.Role == Role.Parent
                ? new ParentChild { ParentId = user.Id, ChildId = invitation.CreatedByUserId.Value }
                : new ParentChild { ParentId = invitation.CreatedByUserId.Value, ChildId = user.Id };
            db.ParentChildren.Add(link);
            await db.SaveChangesAsync();
        }

        await invitationService.RedeemAsync(invitation.Id);
        return user;
    }
}
```

Register in Program.cs: `builder.Services.AddScoped<RegistrationService>();`

- [ ] **Step 5: Write RegistrationService tests**

```csharp
// tests/VokabelTrainer.Api.Tests/Services/RegistrationServiceTests.cs
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;

public class RegistrationServiceTests
{
    [Fact]
    public async Task Register_CreatesUserWithCorrectRole()
    {
        using var db = TestDbContextFactory.Create();
        var creator = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        var invService = new InvitationService(db);
        var inv = await invService.CreateAsync(creator.Id, Role.Parent, linkToCreator: true);
        var invitation = await invService.GetByTokenAsync(inv.Token);

        var regService = new RegistrationService(db, invService);
        var user = await regService.RegisterAsync(invitation!, "mama", "password123");

        user.Should().NotBeNull();
        user!.Role.Should().Be(Role.Parent);
        user.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Register_LinksParentToChild_WhenInvitedByChild()
    {
        using var db = TestDbContextFactory.Create();
        var child = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(child);
        await db.SaveChangesAsync();
        var invService = new InvitationService(db);
        var inv = await invService.CreateAsync(child.Id, Role.Parent, linkToCreator: true);
        var invitation = await invService.GetByTokenAsync(inv.Token);

        var regService = new RegistrationService(db, invService);
        var parent = await regService.RegisterAsync(invitation!, "mama", "password123");

        var link = db.ParentChildren.FirstOrDefault();
        link.Should().NotBeNull();
        link!.ParentId.Should().Be(parent!.Id);
        link.ChildId.Should().Be(child.Id);
    }

    [Fact]
    public async Task Register_LinksChildToParent_WhenInvitedByParent()
    {
        using var db = TestDbContextFactory.Create();
        var parent = new User { Username = "mama", Role = Role.Parent };
        db.Users.Add(parent);
        await db.SaveChangesAsync();
        var invService = new InvitationService(db);
        var inv = await invService.CreateAsync(parent.Id, Role.Child, linkToCreator: true);
        var invitation = await invService.GetByTokenAsync(inv.Token);

        var regService = new RegistrationService(db, invService);
        var child = await regService.RegisterAsync(invitation!, "kid", "password123");

        var link = db.ParentChildren.FirstOrDefault();
        link.Should().NotBeNull();
        link!.ParentId.Should().Be(parent.Id);
        link.ChildId.Should().Be(child!.Id);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsNull()
    {
        using var db = TestDbContextFactory.Create();
        var creator = new User { Username = "kid", Role = Role.Child };
        db.Users.Add(creator);
        await db.SaveChangesAsync();
        var invService = new InvitationService(db);
        var inv = await invService.CreateAsync(creator.Id, Role.Parent, linkToCreator: true);
        var invitation = await invService.GetByTokenAsync(inv.Token);

        var regService = new RegistrationService(db, invService);
        var user = await regService.RegisterAsync(invitation!, "kid", "password123");

        user.Should().BeNull();
    }
}
```

- [ ] **Step 6: Run tests**

Run: `dotnet test --filter RegistrationServiceTests`
Expected: All 4 tests pass.

- [ ] **Step 7: Run build**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```
feat: add invitation-based registration flow with Register page
```

---

### Task 7: ParentService + Tests

**Files:**
- Create: `src/VokabelTrainer.Api/Services/ParentService.cs`
- Create: `tests/VokabelTrainer.Api.Tests/Services/ParentServiceTests.cs`
- Modify: `src/VokabelTrainer.Api/Program.cs` (register service)

- [ ] **Step 1: Write ParentService tests**

Test that the service returns linked children with last activity, and verifies parent-child relationship.

```csharp
namespace VokabelTrainer.Api.Tests.Services;
using FluentAssertions;
using VokabelTrainer.Api.Data.Entities;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Tests.Helpers;

public class ParentServiceTests
{
    [Fact]
    public async Task GetChildren_ReturnsLinkedChildren()
    {
        using var db = TestDbContextFactory.Create();
        var parent = new User { Username = "mama", Role = Role.Parent };
        var child = new User { Username = "kid", Role = Role.Child };
        db.Users.AddRange(parent, child);
        await db.SaveChangesAsync();
        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child.Id });
        await db.SaveChangesAsync();

        var service = new ParentService(db);
        var children = await service.GetChildrenAsync(parent.Id);

        children.Should().HaveCount(1);
        children[0].Username.Should().Be("kid");
    }

    [Fact]
    public async Task GetChildren_ExcludesUnlinkedChildren()
    {
        using var db = TestDbContextFactory.Create();
        var parent = new User { Username = "mama", Role = Role.Parent };
        var child1 = new User { Username = "kid1", Role = Role.Child };
        var child2 = new User { Username = "kid2", Role = Role.Child };
        db.Users.AddRange(parent, child1, child2);
        await db.SaveChangesAsync();
        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child1.Id });
        await db.SaveChangesAsync();

        var service = new ParentService(db);
        var children = await service.GetChildrenAsync(parent.Id);

        children.Should().HaveCount(1);
        children[0].Username.Should().Be("kid1");
    }

    [Fact]
    public async Task IsLinked_ReturnsTrueForLinkedPair()
    {
        using var db = TestDbContextFactory.Create();
        var parent = new User { Username = "mama", Role = Role.Parent };
        var child = new User { Username = "kid", Role = Role.Child };
        db.Users.AddRange(parent, child);
        await db.SaveChangesAsync();
        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child.Id });
        await db.SaveChangesAsync();

        var service = new ParentService(db);
        (await service.IsLinkedAsync(parent.Id, child.Id)).Should().BeTrue();
        (await service.IsLinkedAsync(parent.Id, 999)).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Implement ParentService**

```csharp
namespace VokabelTrainer.Api.Services;
using Microsoft.EntityFrameworkCore;
using VokabelTrainer.Api.Data;
using VokabelTrainer.Api.Data.Entities;

public class ParentService(AppDbContext db)
{
    public async Task<List<ChildSummaryDto>> GetChildrenAsync(int parentId)
    {
        var childIds = await db.ParentChildren
            .Where(pc => pc.ParentId == parentId)
            .Select(pc => pc.ChildId)
            .ToListAsync();

        var children = await db.Users
            .Where(u => childIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.Username,
                LastTraining = db.TrainingSessions
                    .Where(s => s.UserId == u.Id && s.CompletedAt != null)
                    .OrderByDescending(s => s.CompletedAt)
                    .Select(s => s.CompletedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return children.Select(c => new ChildSummaryDto(c.Id, c.Username, c.LastTraining)).ToList();
    }

    public async Task<bool> IsLinkedAsync(int parentId, int childId)
    {
        return await db.ParentChildren.AnyAsync(pc => pc.ParentId == parentId && pc.ChildId == childId);
    }

    public async Task LinkAsync(int parentId, int childId)
    {
        db.ParentChildren.Add(new ParentChild { ParentId = parentId, ChildId = childId });
        await db.SaveChangesAsync();
    }

    public async Task UnlinkAsync(int parentId, int childId)
    {
        var link = await db.ParentChildren.FirstOrDefaultAsync(pc => pc.ParentId == parentId && pc.ChildId == childId);
        if (link is not null)
        {
            db.ParentChildren.Remove(link);
            await db.SaveChangesAsync();
        }
    }
}
```

Also create the DTO file:

```csharp
// src/VokabelTrainer.Api/Models/Parent/ChildSummaryDto.cs
namespace VokabelTrainer.Api.Models.Parent;

public record ChildSummaryDto(int Id, string Username, DateTime? LastTraining);
```

- [ ] **Step 3: Register ParentService in Program.cs**

Add: `builder.Services.AddScoped<ParentService>();`

- [ ] **Step 4: Run tests**

Run: `dotnet test --filter ParentServiceTests`
Expected: All 3 tests pass.

- [ ] **Step 5: Commit**

```
feat: add ParentService for children listing and link management
```

---

### Task 8: Parent Dashboard Endpoints + Pages

**Files:**
- Create: `src/VokabelTrainer.Api/Endpoints/ParentEndpoints.cs`
- Create: `src/VokabelTrainer.Api/Components/Pages/ParentDashboard.razor`
- Create: `src/VokabelTrainer.Api/Components/Pages/ParentChildProgress.razor`
- Modify: `src/VokabelTrainer.Api/Program.cs` (map endpoints)

- [ ] **Step 1: Create ParentEndpoints**

```csharp
using Microsoft.AspNetCore.Http.HttpResults;
using VokabelTrainer.Api.Components.Pages;
using VokabelTrainer.Api.Models;
using VokabelTrainer.Api.Services;

namespace VokabelTrainer.Api.Endpoints;

public static class ParentEndpoints
{
    public static WebApplication MapParentEndpoints(this WebApplication app)
    {
        app.MapGet("/parent", async (ParentService parentService, HttpContext ctx) =>
        {
            var userId = ctx.GetUserId();
            var children = await parentService.GetChildrenAsync(userId);

            return new RazorComponentResult<ParentDashboard>(new
            {
                Children = children,
                IsAdmin = ctx.User.IsInRole("Admin"),
                Role = ctx.GetRole()
            });
        }).RequireAuthorization(Policies.ParentOnly);

        app.MapGet("/parent/child/{childId:int}", async (int childId, ParentService parentService,
            ProgressService progressService, HttpContext ctx) =>
        {
            var parentId = ctx.GetUserId();
            if (!await parentService.IsLinkedAsync(parentId, childId))
                return Results.Forbid();

            var progress = await progressService.GetGlobalProgressAsync(childId);
            var child = (await parentService.GetChildrenAsync(parentId)).FirstOrDefault(c => c.Id == childId);

            return new RazorComponentResult<ParentChildProgress>(new
            {
                ChildName = child?.Username ?? "",
                ChildId = childId,
                ProgressData = progress,
                IsAdmin = ctx.User.IsInRole("Admin"),
                Role = ctx.GetRole()
            });
        }).RequireAuthorization(Policies.ParentOnly);

        app.MapGet("/parent/child/{childId:int}/list/{listId:int}", async (int childId, int listId,
            ParentService parentService, ProgressService progressService, HttpContext ctx) =>
        {
            var parentId = ctx.GetUserId();
            if (!await parentService.IsLinkedAsync(parentId, childId))
                return Results.Forbid();

            var progress = await progressService.GetListProgressAsync(childId, listId);
            var child = (await parentService.GetChildrenAsync(parentId)).FirstOrDefault(c => c.Id == childId);

            return new RazorComponentResult<ParentChildProgress>(new
            {
                ChildName = child?.Username ?? "",
                ChildId = childId,
                ProgressData = progress,
                IsAdmin = ctx.User.IsInRole("Admin"),
                Role = ctx.GetRole()
            });
        }).RequireAuthorization(Policies.ParentOnly);

        return app;
    }
}
```

- [ ] **Step 2: Create ParentDashboard.razor**

Shows list of linked children with last activity, "Kind einladen" button, empty state.

```razor
<PageLayout IsAdmin="@IsAdmin" Role="@Role">
    <h1 class="title">Meine Kinder</h1>

    <div class="mb-4">
        <form method="post" action="/invitations/create-child">
            <button type="submit" class="button is-primary">+ Kind einladen</button>
        </form>
    </div>

    @if (Children.Count == 0)
    {
        <div class="notification is-info">
            Noch keine Kinder verknüpft. Erstelle einen Einladungslink, um ein Kind zu verbinden.
        </div>
    }
    else
    {
        <div class="columns is-multiline">
            @foreach (var child in Children)
            {
                <div class="column is-6">
                    <div class="card">
                        <div class="card-content">
                            <p class="title is-5">@child.Username</p>
                            <p class="has-text-grey">
                                @if (child.LastTraining.HasValue)
                                {
                                    <span>Letztes Training: @child.LastTraining.Value.ToString("dd.MM.yyyy HH:mm")</span>
                                }
                                else
                                {
                                    <span>Noch kein Training absolviert</span>
                                }
                            </p>
                        </div>
                        <footer class="card-footer">
                            <a href="/parent/child/@child.Id" class="card-footer-item">Fortschritt ansehen</a>
                        </footer>
                    </div>
                </div>
            }
        </div>
    }
</PageLayout>

@code {
    [Parameter] public List<ChildSummaryDto> Children { get; set; } = [];
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public Role Role { get; set; }
}
```

- [ ] **Step 3: Create ParentChildProgress.razor**

Reuses the same structure as `Progress.razor` but with a "Zurück" link to `/parent` and child name in title:

```razor
<PageLayout IsAdmin="@IsAdmin" Role="@Role">
    @if (ProgressData is null)
    {
        <div class="notification is-warning">Keine Daten gefunden.</div>
        <a href="/parent" class="button">Zurück</a>
    }
    else
    {
        <h1 class="title">Fortschritt: @ChildName</h1>
        <p class="subtitle">@ProgressData.ListName</p>

        <LeitnerExplanation />

        <div class="box">
            <h2 class="subtitle">Box-Verteilung</h2>
            <BoxDistribution Distribution="@ProgressData.BoxDistribution" />
            <div class="columns mt-3 has-text-centered">
                <div class="column"><span class="tag is-danger">Box 1: @ProgressData.BoxDistribution.Box1</span></div>
                <div class="column"><span class="tag is-warning">Box 2: @ProgressData.BoxDistribution.Box2</span></div>
                <div class="column"><span class="tag is-info">Box 3: @ProgressData.BoxDistribution.Box3</span></div>
                <div class="column"><span class="tag is-primary">Box 4: @ProgressData.BoxDistribution.Box4</span></div>
                <div class="column"><span class="tag is-success">Box 5: @ProgressData.BoxDistribution.Box5</span></div>
            </div>
        </div>

        <div class="box">
            <h2 class="subtitle">Trainingshistorie</h2>
            <p class="mb-3">Gesamt: <strong>@ProgressData.TotalSessions</strong> Sitzungen</p>
            @if (ProgressData.SessionHistory.Count > 0)
            {
                <table class="table is-fullwidth is-striped">
                    <thead><tr><th>#</th><th>Datum</th><th>Erfolgsquote</th></tr></thead>
                    <tbody>
                        @for (var i = 0; i < ProgressData.SessionHistory.Count; i++)
                        {
                            var session = ProgressData.SessionHistory[i];
                            <tr>
                                <td>@(i + 1)</td>
                                <td>@session.Date.ToString("dd.MM.yyyy HH:mm")</td>
                                <td>@session.SuccessRate.ToString("0.0")%</td>
                            </tr>
                        }
                    </tbody>
                </table>
            }
            else
            {
                <p class="has-text-grey">Noch keine Sitzungen absolviert.</p>
            }
        </div>

        @if (ProgressData.ProblemVocabulary.Count > 0)
        {
            <div class="box">
                <h2 class="subtitle">Problemvokabeln</h2>
                <table class="table is-fullwidth is-striped">
                    <thead><tr><th>Begriff</th><th>Falsch-Anzahl</th><th>Aktuelle Box</th></tr></thead>
                    <tbody>
                        @foreach (var vocab in ProgressData.ProblemVocabulary)
                        {
                            <tr>
                                <td>
                                    @vocab.Term
                                    @if (vocab.Hint is not null)
                                    {
                                        <br/><small class="has-text-info">@vocab.Hint</small>
                                    }
                                </td>
                                <td>@vocab.TimesWrong</td>
                                <td>@vocab.CurrentBox</td>
                            </tr>
                        }
                    </tbody>
                </table>
            </div>
        }

        <a href="/parent" class="button">Zurück</a>
    }
</PageLayout>

@code {
    [Parameter] public string ChildName { get; set; } = "";
    [Parameter] public int ChildId { get; set; }
    [Parameter] public ListProgressDto? ProgressData { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public Role Role { get; set; }
}
```

- [ ] **Step 4: Register endpoints in Program.cs**

Add: `app.MapParentEndpoints();`

- [ ] **Step 5: Run build**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```
feat: add parent dashboard with child progress views
```

---

### Task 9: PageLayout + Navbar + Invitation UI for Children

**Files:**
- Modify: `src/VokabelTrainer.Api/Components/PageLayout.razor`
- Modify: `src/VokabelTrainer.Api/Components/Pages/Dashboard.razor`
- Modify: `src/VokabelTrainer.Api/Endpoints/DashboardEndpoints.cs`
- Create invitation endpoints for child/parent invite creation

- [ ] **Step 1: Update PageLayout.razor — add Role parameter, role-based navbar**

Add `Role` parameter. Conditionally render navbar items:
- Child: Dashboard, Fortschritt, Hilfe
- Parent: Meine Kinder, Hilfe
- Admin items (Benutzer, Sprachen) shown regardless of role when `IsAdmin`

```razor
@* Update navbar-start section: *@
<div class="navbar-start">
    @if (Role == Models.Role.Child)
    {
        <a class="navbar-item" href="/">Dashboard</a>
        <a class="navbar-item" href="/progress">Fortschritt</a>
    }
    else if (Role == Models.Role.Parent)
    {
        <a class="navbar-item" href="/parent">Meine Kinder</a>
    }
    @if (IsAdmin)
    {
        <a class="navbar-item" href="/admin/users">Benutzer</a>
        <a class="navbar-item" href="/admin/languages">Sprachen</a>
    }
</div>

@* Update @code block: *@
@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool ShowNavbar { get; set; } = true;
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public Role Role { get; set; }
}
```

- [ ] **Step 2: Update Dashboard.razor — add Role parameter, invite button**

Add `Role` parameter to pass through to `PageLayout`. Add "Eltern einladen" button:

```razor
<PageLayout IsAdmin="@IsAdmin" Role="@Role">
    @* ... existing content ... *@

    @* Add after the level-right buttons: *@
    <div class="mt-4">
        <form method="post" action="/invitations/create-parent">
            <button type="submit" class="button is-outlined">Eltern einladen</button>
        </form>
    </div>

    @* ... rest ... *@
</PageLayout>

@code {
    [Parameter] public List<VocabularyListSummaryDto> Lists { get; set; } = [];
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public Role Role { get; set; }
}
```

- [ ] **Step 3: Update DashboardEndpoints to pass Role**

```csharp
return new RazorComponentResult<Dashboard>(new
{
    Lists = lists,
    IsAdmin = ctx.User.IsInRole("Admin"),
    Role = ctx.GetRole()
});
```

- [ ] **Step 4: Add invitation creation endpoints**

These can go in a new section of AdminEndpoints or as standalone. Create `POST /invitations/create-parent` (for children inviting parents) and `POST /invitations/create-child` (for parents inviting children):

```csharp
// In a suitable endpoints file — or add to AdminEndpoints:
app.MapPost("/invitations/create-parent", async (HttpContext ctx, InvitationService invitationService) =>
{
    var userId = ctx.GetUserId();
    var invitation = await invitationService.CreateAsync(userId, Role.Parent, linkToCreator: true);
    var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/register?invite={invitation.Token}";
    // Show the URL to the user — redirect to a page or return inline
    return Results.Redirect($"/?invite-url={Uri.EscapeDataString(url)}");
}).RequireAuthorization(Policies.ChildOnly);

app.MapPost("/invitations/create-child", async (HttpContext ctx, InvitationService invitationService) =>
{
    var userId = ctx.GetUserId();
    var invitation = await invitationService.CreateAsync(userId, Role.Child, linkToCreator: true);
    var url = $"{ctx.Request.Scheme}://{ctx.Request.Host}/register?invite={invitation.Token}";
    return Results.Redirect($"/parent?invite-url={Uri.EscapeDataString(url)}");
}).RequireAuthorization(Policies.ParentOnly);
```

Update the Dashboard.razor and ParentDashboard.razor to display the invite URL when the `invite-url` query parameter is present (show in a notification box with copyable text).

- [ ] **Step 5: Update all existing full-page GET endpoints to pass Role**

Every endpoint that returns a `RazorComponentResult` with `IsAdmin` also needs `Role = ctx.GetRole()`. Update these endpoints:
- `ListEndpoints` (GET /lists/new, GET /lists/{id}/edit)
- `TrainingEndpoints` (GET /training/start, GET /training/{sessionId}, GET /training/result/{sessionId})
- `ProgressEndpoints` (GET /progress, GET /progress/{listId})
- `AdminEndpoints` (GET /admin/users, GET /admin/languages)
- Help endpoint in Program.cs — **Note:** The help endpoint uses `.RequireAuthorization()` (no specific policy), so `ctx.GetRole()` is safe here since the user is always authenticated. Add `Role = ctx.GetRole()`.

And update each corresponding `.razor` page to accept and forward the `Role` parameter to `PageLayout`.

- [ ] **Step 6: Run build**

Run: `dotnet build`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```
feat: role-based navbar, invitation creation UI, pass Role to all pages
```

---

### Task 10: Endpoint Policy Migration

**Files:**
- Modify: `src/VokabelTrainer.Api/Endpoints/DashboardEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/ListEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/TrainingEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/ProgressEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Endpoints/AdminEndpoints.cs`

- [ ] **Step 1: Add ChildOnly policy to child endpoints**

Replace `.RequireAuthorization()` with `.RequireAuthorization(Policies.ChildOnly)` on:
- All endpoints in DashboardEndpoints
- All endpoints in ListEndpoints
- All endpoints in TrainingEndpoints
- All endpoints in ProgressEndpoints

- [ ] **Step 2: Replace "AdminOnly" strings with Policies.AdminOnly**

In AdminEndpoints, replace all `"AdminOnly"` strings with `Policies.AdminOnly`.

- [ ] **Step 3: Run tests**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```
feat: enforce ChildOnly/ParentOnly/AdminOnly policies on all endpoints
```

---

### Task 11: Admin — Invitation Management + ParentChild Management

**Files:**
- Modify: `src/VokabelTrainer.Api/Endpoints/AdminEndpoints.cs`
- Modify: `src/VokabelTrainer.Api/Components/Pages/Admin/UserManagement.razor`

- [ ] **Step 1: Add admin invitation endpoints**

```csharp
// Admin creates invitation (any role, no auto-link)
app.MapPost("/admin/invitations", async (HttpContext ctx, InvitationService invitationService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var roleStr = form["Role"].FirstOrDefault() ?? "Child";
    var role = Enum.Parse<Role>(roleStr);
    var userId = ctx.GetUserId();
    await invitationService.CreateAsync(userId, role, linkToCreator: false);
    return Results.Redirect("/admin/users");
}).RequireAuthorization(Policies.AdminOnly).DisableAntiforgery();

app.MapPost("/admin/invitations/{id:int}/delete", async (int id, InvitationService invitationService) =>
{
    await invitationService.DeleteAsync(id);
    return Results.Redirect("/admin/users");
}).RequireAuthorization(Policies.AdminOnly).DisableAntiforgery();

// ParentChild management
app.MapPost("/admin/links", async (HttpContext ctx, ParentService parentService) =>
{
    var form = await ctx.Request.ReadFormAsync();
    if (int.TryParse(form["ParentId"].FirstOrDefault(), out var parentId)
        && int.TryParse(form["ChildId"].FirstOrDefault(), out var childId))
    {
        await parentService.LinkAsync(parentId, childId);
    }
    return Results.Redirect("/admin/users");
}).RequireAuthorization(Policies.AdminOnly).DisableAntiforgery();

app.MapPost("/admin/links/{parentId:int}/{childId:int}/delete", async (int parentId, int childId, ParentService parentService) =>
{
    await parentService.UnlinkAsync(parentId, childId);
    return Results.Redirect("/admin/users");
}).RequireAuthorization(Policies.AdminOnly).DisableAntiforgery();
```

- [ ] **Step 2: Update admin GET endpoint to include invitations and links**

Pass invitation list and parent-child links to UserManagement page.

- [ ] **Step 3: Rewrite UserManagement.razor**

Three sections:
1. **Benutzer** — table with Role, IsAdmin, edit/delete
2. **Einladungen** — create form (role dropdown), list of open invitations with delete
3. **Zuordnungen** — list of ParentChild links with delete, form to create new link (parent + child dropdowns)

- [ ] **Step 4: Run build + test**

Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```
feat: admin invitation management and ParentChild assignment UI
```

---

### Task 12: Help Page Update

**Files:**
- Modify: `src/VokabelTrainer.Api/Components/Pages/Help.razor`

- [ ] **Step 1: Update Help.razor**

Add new sections (in du-form, no jargon):

- **Rollen** section: "Es gibt zwei Rollen: Kind und Elternteil. Als Kind kannst du Vokabeln lernen, Listen erstellen und trainieren. Eltern können den Lernfortschritt ihrer Kinder einsehen, aber nicht selbst trainieren oder Listen bearbeiten."
- **Einladungen** section: "Um jemanden einzuladen, klicke auf 'Eltern einladen' (als Kind) oder 'Kind einladen' (als Elternteil). Du erhältst einen Link, den du weitergeben kannst. Jeder Link kann zweimal verwendet werden."
- Add Role parameter, update PageLayout call to include Role.
- Update table of contents.

- [ ] **Step 2: Commit**

```
docs: update help page with parent roles and invitation info
```

---

### Task 13: Final Cleanup + Test Run

**Files:**
- Various — cleanup any remaining `UserRole` references
- Modify: `src/VokabelTrainer.Api/Models/Auth/LoginRequest.cs` (keep or delete if unused)

- [ ] **Step 1: Search for remaining UserRole references**

Run grep for `UserRole` across the entire project. Fix any remaining references.

- [ ] **Step 2: Search for remaining IsInitialized references**

Run grep for `IsInitialized` across the entire project. Fix any remaining references.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test`
Expected: All tests pass (including new InvitationService and ParentService tests).

- [ ] **Step 4: Run the app and smoke-test**

Run: `dotnet run --project src/VokabelTrainer.Api`

Manual verification:
1. First-user setup still works (creates admin with Role.Child + IsAdmin)
2. Admin can create invitation links
3. Registration via invitation link works
4. Child sees "Eltern einladen" button
5. Parent sees "Meine Kinder" dashboard
6. Parent can view child's progress
7. Parent cannot access training/lists endpoints (gets 403)
8. Admin can manage users, invitations, ParentChild links

- [ ] **Step 5: Commit**

```
chore: cleanup remaining UserRole/IsInitialized references
```

- [ ] **Step 6: Update CLAUDE.md**

Add to Key Design Decisions:
- **Role-based access**: `Role` enum (Child, Parent) for app-level roles, `IsAdmin` bool for admin access. Authorization via `Policies.ChildOnly`/`Policies.ParentOnly`/`Policies.AdminOnly` constants.
- **Invitation-based registration**: Token-based invitations replace admin-creates-user flow. Links are valid for 2 uses, no expiration.

Update Project Structure section with new files.

- [ ] **Step 7: Commit**

```
docs: update CLAUDE.md with parent access architecture
```
