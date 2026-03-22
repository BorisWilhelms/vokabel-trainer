# Parent Access & Dashboard Design

**Goal:** Add parent accounts that can view their children's learning progress (read-only), with an invitation-based registration system replacing the current admin-creates-user flow.

## Data Model

### User Entity Changes

Replace `UserRole Role` with:

- `Role Role` — new enum: `Child`, `Parent` (extensible for future `Teacher`)
- `bool IsAdmin` — orthogonal admin flag (any role can be admin)

Remove: `UserRole` enum (deleted entirely).
Remove: `IsInitialized` field (all existing users are initialized; the first-login password flow is replaced by invitation-based registration).

Migration for existing users:
- `UserRole.User` → `Role = Child, IsAdmin = false`
- `UserRole.Admin` → `Role = Child, IsAdmin = true`

### New Entity: ParentChild

Join table for n:m parent-child relationships.

| Column   | Type | Constraint                      |
|----------|------|---------------------------------|
| ParentId | int  | FK → User, cascade delete       |
| ChildId  | int  | FK → User, cascade delete       |
|          |      | Unique(ParentId, ChildId)       |

Service-level validation ensures `ParentId` references a user with `Role = Parent` and `ChildId` references a user with `Role = Child`.

### New Entity: Invitation

| Column          | Type     | Description                                              |
|-----------------|----------|----------------------------------------------------------|
| Id              | int      | PK                                                       |
| Token           | string   | Unique, URL-safe, 32 chars                               |
| Role            | Role     | Role assigned to registering user                        |
| CreatedByUserId | int?     | FK → User (nullable, cascade set null on user delete)    |
| UsesRemaining   | int      | Default 2, decremented on each registration              |
| LinkToCreator   | bool     | Auto-create ParentChild relationship with creator        |
| CreatedAt       | DateTime |                                                          |

No expiration by design — invitations remain valid until uses are exhausted or admin deletes them.

### New Enum: Role

```csharp
public enum Role { Child, Parent }
```

## Authentication & Claims

### Claim Emission

On login and registration, emit claims:

- `ClaimTypes.NameIdentifier` → User.Id (unchanged)
- `ClaimTypes.Name` → User.Username (unchanged)
- `"Role"` → User.Role.ToString() (`"Child"` or `"Parent"`)
- `ClaimTypes.Role` → `"Admin"` (only if `User.IsAdmin == true`)

This preserves `ctx.User.IsInRole("Admin")` for admin checks. The `"AdminOnly"` policy continues to use `RequireRole("Admin")`. The user's `Role` (Child/Parent) is read from the `"Role"` claim via a helper method.

**Important:** `ctx.User.IsInRole("Child")` and `ctx.User.IsInRole("Parent")` will NOT work because those values are on the custom `"Role"` claim, not `ClaimTypes.Role`. Always use the `ctx.GetRole()` helper to check Child/Parent.

### AuthResponse DTO

`AuthResponse` is updated to use the new `Role` enum and `bool IsAdmin` instead of `UserRole`:

```csharp
public record AuthResponse(int Id, string Username, Role Role, bool IsAdmin);
```

### OnValidatePrincipal

Extend the existing `OnValidatePrincipal` to refresh claims from DB on each request. Currently it only checks if the user exists; after the change it rebuilds the `ClaimsPrincipal` from the current DB state. This ensures that admin-initiated changes to `Role` or `IsAdmin` take effect without requiring re-login.

### Post-Login Redirect

The login POST endpoint (`AuthEndpoints`) checks the user's `Role` after successful authentication:
- `Role.Child` → redirect to `/`
- `Role.Parent` → redirect to `/parent`

## Registration & Invitation Flow

### Registration Page

`GET /register?invite={token}` — public endpoint (no auth required).

1. Validate token: exists and `UsesRemaining > 0`. If invalid, show error — no form.
2. Show form: Username + Password. Role is derived from invitation (not user-selectable).
3. On submit (`POST /register`):
   - Create User with `Role` from invitation, `IsAdmin = false`
   - Decrement `UsesRemaining`
   - If `LinkToCreator = true`: create ParentChild record with explicit direction:
     - Invitation `Role = Parent` (new user is parent, creator is child) → `ParentId = newUser.Id, ChildId = creator.Id`
     - Invitation `Role = Child` (new user is child, creator is parent) → `ParentId = creator.Id, ChildId = newUser.Id`
   - Log user in, redirect based on role (see Post-Login Redirect)

### Who Creates Invitations

| Creator  | Creates invitation for | LinkToCreator | UsesRemaining |
|----------|------------------------|---------------|---------------|
| Child    | Parent                 | true          | 2             |
| Parent   | Child                  | true          | 2             |
| Admin    | Child or Parent        | false         | 2             |

- Child creates "Eltern einladen" → `Role=Parent, LinkToCreator=true`
- Parent creates "Kind einladen" → `Role=Child, LinkToCreator=true`
- Admin creates from user management → role selectable, `LinkToCreator=false`, assignment done manually

### First-User Bootstrap

The existing first-user-becomes-admin flow (`AuthService.LoginOrSetupAsync` — if no users exist, first login creates admin) is preserved but adapted:
- First user is created with `Role = Child, IsAdmin = true` (instead of `UserRole.Admin`)
- The login page continues to show the initial setup form when no users exist
- After the first admin is created, all further users register via invitation links

### Removal of Legacy User Creation

The admin-creates-user-with-username flow (where users set passwords on first login via `IsInitialized`) is removed entirely. The `IsInitialized` field and `AuthService.LoginOrSetupAsync`'s `IsInitialized` code path are deleted in the same migration. All new user onboarding (after the first admin) goes through invitation links.

## Routing & Dashboards

### Parent Dashboard

**`GET /parent`** — list of linked children:
- Per child: name, last training date
- Click → drill down to child detail
- Button: "Kind einladen"
- Empty state: message explaining no children linked yet, with "Kind einladen" button

**`GET /parent/child/{childId}`** — same data as existing Progress page:
- Box distribution (all lists combined)
- Session history (date, success rate)
- Problem vocabularies (top 10 wrong answers)
- Per-list breakdown available via `GET /parent/child/{childId}/list/{listId}`

**Access control:** All `/parent/child/{childId}` endpoints verify a `ParentChild` record exists for the logged-in parent and the requested child. 403 if not linked.

### Child Dashboard Changes

- New button: "Eltern einladen" — creates invitation, shows copyable link
- Rest of dashboard unchanged

### Navbar & PageLayout

`PageLayout.razor` receives a new `Role` parameter (in addition to existing `IsAdmin`):

| Role   | Navbar items                                           |
|--------|--------------------------------------------------------|
| Child  | Startseite, Fortschritt, Hilfe, (Eltern einladen)     |
| Parent | Meine Kinder, Hilfe, (Kind einladen)                   |
| Admin  | + Admin link (regardless of role)                       |

Parents have no access to training, lists, or personal progress — they have no vocabulary data.

## Authorization Changes

### Endpoint Protection

### Policy Constants & Registration

Policy names are defined as constants to avoid magic strings:

```csharp
public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string ChildOnly = nameof(ChildOnly);
    public const string ParentOnly = nameof(ParentOnly);
}
```

Registration:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole("Admin"))
    .AddPolicy(Policies.ChildOnly, p => p.RequireClaim("Role", "Child"))
    .AddPolicy(Policies.ParentOnly, p => p.RequireClaim("Role", "Parent"));
```

Usage: `.RequireAuthorization(Policies.ChildOnly)`, `.RequireAuthorization(Policies.AdminOnly)`, etc.

### Endpoint → Policy Mapping

Child-only endpoints use `Policies.ChildOnly` to prevent parents from accessing them directly:
- `GET /` (dashboard), `GET /lists/*`, `POST /lists/*`, `GET /training/*`, `POST /training/*`, `GET /progress/*`

Parent-only endpoints use `Policies.ParentOnly`:
- `GET /parent`, `GET /parent/child/*`

Shared endpoints (no role restriction, just `.RequireAuthorization()`):
- `GET /help`

Public endpoints (no auth):
- `GET /login`, `POST /login`, `GET /register`, `POST /register`

Admin endpoints use `Policies.AdminOnly` (checks `IsAdmin` via `ClaimTypes.Role = "Admin"`).

### Helper Methods

- `ctx.GetUserId()` — unchanged
- `ctx.GetRole()` — new, reads `"Role"` claim, returns `Role` enum
- `ctx.User.IsInRole("Admin")` — unchanged, works via `ClaimTypes.Role` claim

All full-page GET endpoints pass both `IsAdmin` and `Role` to `PageLayout`.

## Admin Changes

### User Management

- User list shows: Username, Role (Kind/Elternteil), IsAdmin
- "Benutzer anlegen" replaced by "Einladungslink erstellen" (role selectable)
- Admin can change Role and IsAdmin per user
- New section: ParentChild assignments (create/delete links between existing users)

### Invitation Management

- List of open invitations: Token, Role, Created By, UsesRemaining, Created At
- Admin can delete/invalidate invitations

## Help Page

Update with (in du-form, no jargon):
- What parent and child roles mean
- What parents can and cannot see
- How invitation links work (creating, sharing, two uses)
- How to link parents and children

## Out of Scope

- Parents editing children's data (explicitly read-only)
- Email/notification when child completes training
- Teacher role (future extension point exists via Role enum)
- Self-registration without invitation
