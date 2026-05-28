# FamilySplit — Architecture Guide

> Reference for Claude (and human developers). Keep this file up-to-date as the architecture evolves.

---

## Project Overview

FamilySplit is a family expense-splitting app where costs are divided by **age-weighted shares** rather than equal splits. Multiple *Families* (household units) join shared *Groups*, log activities and expenses, and the app calculates fair per-Family settlement amounts automatically.

**Tech stack:** .NET 10 · Blazor WebAssembly · ASP.NET Core Minimal API · PostgreSQL · Entity Framework Core 10 · MudBlazor 8 · Fluxor 6 · Refit 8 · FluentValidation 11 · Serilog

---

## Solution Structure

```
src/
├── FamilySplit.Domain          # Entities, enums — no dependencies
├── FamilySplit.Infrastructure  # EF Core DbContext, migrations, entity configs
├── FamilySplit.Application     # Service layer, DTOs, validators
├── FamilySplit.Api             # ASP.NET Core Minimal API host
└── FamilySplit.Client          # Blazor WebAssembly SPA
```

---

## Core Domain Concepts

### Identity: User → FamilyMember → Family

| Entity | Purpose |
|---|---|
| `User` | OAuth identity shell — holds provider credentials, email, display name, avatar |
| `FamilyMember` | A participant in expense splits — holds DOB, weight, family membership |
| `Family` | A household unit — the settlement target (Family A owes Family B €X) |

**Key rules:**
- A `FamilyMember` always belongs to exactly one `Family` (`FamilyMember.FamilyId` is required, FK with Cascade delete).
- A `FamilyMember` can exist **without** a `User` (e.g., a child with no account).
- A `User` can only log in if a `FamilyMember` with a matching email exists.
- On first login, `OAuthHandler` matches `User.Email` → `FamilyMember.Email` and sets `FamilyMember.UserId`.
- Email is unique on `FamilyMember` (filtered index — only enforced when email is not null).
- The `User`↔`FamilyMember` relationship is **1:0..1** (one-to-one optional).
- `FamilyMember.IsAdmin` — whether this member is an admin of their own Family (can add/update/remove siblings).
- `User.IsGlobalAdmin` — super-admin flag set directly in the DB; allows creating Families and managing any FamilyMember.

### Global Admin Bootstrap

Since login is gated on having a linked `FamilyMember`, the setup flow for a fresh environment is:

**Step 1 — Seed the first family and member directly in the DB:**
```sql
-- Create the family
INSERT INTO families (id, name, created_at, updated_at)
VALUES (gen_random_uuid(), 'Admin Family', now(), now());

-- Create the admin member (replace the family_id and email)
INSERT INTO family_members (id, family_id, display_name, email, is_admin, is_active, created_at)
VALUES (gen_random_uuid(), '<family-id from above>', 'Your Name', 'you@example.com', true, true, now());
```

**Step 2 — Log in with Google.** `OAuthHandler` will match the email, link `FamilyMember.UserId`, and issue a JWT.

**Step 3 — Promote to global admin:**
```sql
UPDATE users SET is_global_admin = true WHERE email = 'you@example.com';
```

**Step 4** — Use `POST /admin/families` and `POST /admin/families/{id}/members` to create additional Families and seed their members through the API.

### Groups and GroupFamilies

A `Group` is a named set of `Family` units who share expenses.

```
Group ──< GroupFamily >── Family ──< FamilyMember
```

- `GroupFamily` replaces the old `GroupMembership`. It links `Group` ↔ `Family` (not individual members).
- `GroupFamily.Role` (`Admin` or `Member`) is a per-Family role within the Group.
- When a Family joins a Group via invite code, **all active FamilyMembers** participate automatically in expenses.
- Settlement is computed **per Family** — individual member balances are summed per Family, then settled between Families.
- Only the **caller's Family** can join via invite code (Family-level join).

### Weight Tiers (WeightTier enum)

Expenses are split by weight, calculated from date of birth at expense-save time:

| Tier | Age range | Weight |
|---|---|---|
| `Kleuterschool` (0) | 0–5 | 0.25 |
| `LagerOnderwijs` (1) | 6–11 | 0.50 |
| `MiddelbaarOnderwijs` (2) | 12–17 | 0.75 |
| `Volwassene` (3) | 18+ | 1.00 |
| `Override` (4) | — | `WeightOverride` value |

`WeightCalculator.GetWeight(member, date)` and `GetTier(member, date)` are the canonical calculation methods (stateless, Application layer).

---

## Authentication Flow

Two-token design — a short-lived **access token** (JWT, 15 minutes) in the WASM client's memory, plus a long-lived **refresh token** (30 days) in an HttpOnly Secure SameSite=Strict cookie scoped to `/auth`. Neither token ever touches `localStorage` or `sessionStorage`.

1. Client calls `GET /auth/login/Google?returnUrl=...`
2. API generates PKCE flow, stores state in an encrypted HttpOnly cookie, redirects to Google.
3. Google redirects back to `GET /auth/callback/Google?code=...`
4. `OAuthHandler` exchanges the code, fetches Google userinfo, upserts the `User` row.
5. **FamilyMember check:** looks up `FamilyMember` by email. If none found → redirects to `/not-registered`. If found, links `FamilyMember.UserId = user.Id` (first login only).
6. `RefreshTokenService.IssueAsync` creates a new `refresh_tokens` row (only the SHA-256 hash is stored). The plaintext secret is dropped into the `fs_refresh` cookie (`HttpOnly; Secure; SameSite=Strict; Path=/auth`). The browser is redirected to `/auth/return` on the client.
7. `AuthReturn.razor` immediately calls `POST /auth/refresh` with `credentials: 'include'`. The endpoint rotates the refresh row (marks the old `revoked_at`, `replaced_by_token_id` → new row id) and returns `{ token, expiresInSeconds }`.
8. `AuthService` stores the JWT in memory only. `JwtAuthHandler` attaches it as `Authorization: Bearer …` on every authenticated call.

**Silent refresh:**
- On app boot, `AuthService.TryRefreshAsync()` is dispatched from `CheckAuthAction`. If the refresh cookie is still valid (default on any prior signed-in session), a JWT is obtained without any UI.
- When a JWT call returns 401, `JwtAuthHandler` makes one silent refresh and retries the request once. Persistent 401 → user is treated as signed out.
- `AuthService.GetTokenAsync` proactively refreshes when the cached JWT has <30 seconds of life left.

**Theft detection:** if `/auth/refresh` is presented with a token whose row is already revoked, `RefreshTokenService` invokes `RevokeAllForUserAsync` — every active session for that user is killed immediately.

**Sign-out:** `POST /auth/logout` revokes the presented refresh row server-side and clears the cookie. `AuthService.ClearTokenInMemory()` drops the in-memory JWT.

**JWT claims:** `sub` = `User.Id` (Guid). All service methods receive `callerId` as a `Guid` (the User.Id).

**Data Protection key ring:** persisted to the `DataProtectionKeys` table via `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (`AppDbContext : IDataProtectionKeyContext`). The PKCE state cookie cannot be decrypted without these keys, so they must survive restarts. The old `.dp-keys` folder is no longer used.

### Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET`  | `/auth/login/{provider}` | Start OAuth (PKCE state cookie). |
| `GET`  | `/auth/callback/{provider}` | Exchange code, issue refresh cookie, redirect to client. |
| `POST` | `/auth/refresh` | Rotate refresh cookie + return a fresh JWT. |
| `POST` | `/auth/logout` | Revoke refresh row server-side + clear cookie. |

---

## API Layer (FamilySplit.Api)

### Conventions
- Minimal API, no MediatR.
- `AppDbContext` and service classes injected directly into endpoint lambdas.
- `ClaimsPrincipalExtensions.GetUserId()` extracts `sub` claim as `Guid`.
- `ValidationExceptionMiddleware` catches `FluentValidation.ValidationException` → HTTP 422.
- `ValidationExceptionMiddleware` catches `ForbiddenException` → HTTP 403 (checked **before** ValidationException).

### Endpoint Groups

| File | Prefix | Description |
|---|---|---|
| `AuthEndpoints.cs` | `/auth` | Login, callback, handoff |
| `UserEndpoints.cs` | `/users/me` | WhoAmI (current user info) |
| `FamilyMembersEndpoints.cs` | `/users/me/profile` | Caller's own FamilyMember profile (GET) |
| `AdminEndpoints.cs` | `/admin/families` | Global-admin Family + member CRUD |
| `FamilyEndpoints.cs` | `/families/mine` | Own-family management (rename, members) |
| `GroupsEndpoints.cs` | `/groups` | Group CRUD, join, invite code regeneration |
| `GroupMembersEndpoints.cs` | — | No-op stub (replaced by Family endpoints) |
| `ActivityEndpoints.cs` | `/groups/{groupId}/activities` | Activity CRUD, sub-activities, close, participant add/remove |
| `ExpenseEndpoints.cs` | `/groups/{groupId}/activities/{activityId}/expenses` | Expense CRUD (list, get, create, update, delete) |
| `SettlementEndpoints.cs` | `/groups/{groupId}/activities/{activityId}/settlements` | Settlement generate, list, detail, confirm-sent, confirm-received; plus `/balances` GET |

### Authorization
All endpoints except `/auth/*`, `/health`, and Scalar/OpenAPI require a valid JWT (`RequireAuthorization()`). Global-admin checks are enforced inside `AdminService.RequireGlobalAdminAsync()`.

---

## Application Layer (FamilySplit.Application)

### Services

**`AdminService`** — global-admin operations (requires `User.IsGlobalAdmin = true`)
- `ListFamiliesAsync(callerId)` → all Families with members
- `GetFamilyAsync(familyId, callerId)` → one Family with members
- `CreateFamilyAsync(req, callerId)` → new Family
- `AddFamilyMemberAsync(familyId, req, callerId)` → new FamilyMember; auto-links User if email matches
- `UpdateFamilyMemberAsync(memberId, req, callerId)` → update any member
- `RemoveFamilyMemberAsync(memberId, callerId)` → soft-delete (`IsActive = false`)

**`FamilyService`** — own-family management
- `GetMyFamilyAsync(callerId)` → caller's Family with all active members
- `GetMyProfileAsync(callerId)` → caller's own FamilyMember
- `UpdateFamilyNameAsync(req, callerId)` → admin only
- `AddMemberAsync(req, callerId)` → admin only; auto-links User if email matches
- `UpdateMemberAsync(memberId, req, callerId)` → admin or self
- `RemoveMemberAsync(memberId, callerId)` → admin only; cannot remove self

**`GroupService`** — group-level operations
- `ListAsync(callerId)` → groups the caller's Family belongs to
- `GetDetailAsync(groupId, callerId)` → detail with all participating Families and their active members
- `CreateAsync(req, callerId)` → creates group + Admin GroupFamily for caller's Family
- `UpdateAsync(groupId, req, callerId)` → Admin only
- `JoinAsync(req, callerId)` → join via invite code (adds Member GroupFamily for caller's Family)
- `RegenerateInviteCodeAsync(groupId, callerId)` → Admin only

**`ExpenseService`** — expense operations (any group member)
- `ListAsync(activityId, callerId)` → `List<ExpenseSummaryDto>`
- `GetDetailAsync(expenseId, callerId)` → `ExpenseDetailDto`
- `CreateAsync(activityId, req, callerId)` → `ExpenseDetailDto` — seeds `ExpenseParticipant` rows from current `ActivityParticipant` list, snapshots `WeightCalculator.GetWeight()` at `ExpenseDate`, then runs `SplitCalculator.CalculateShares()`
- `UpdateAsync(expenseId, req, callerId)` → `ExpenseDetailDto` — if `TotalAmount` or `ExpenseDate` changed, re-snapshots weights and recalculates shares
- `DeleteAsync(expenseId, callerId)` → `Task` — allowed on non-Settled activity and non-Locked expense

**`SettlementService`** — settlement operations (any group member)
- `GetBalancesAsync(activityId, callerId)` → `List<FamilyBalanceDto>` — read-only net balance per Family (positive = creditor, negative = debtor)
- `GenerateAsync(activityId, callerId)` → `List<SettlementSummaryDto>` — runs `BalanceCalculator` + `SettlementOptimiser`, persists `Settlement` rows; idempotent (returns existing if already generated); marks Activity Settled immediately if all balances are zero
- `ListAsync(activityId, callerId)` → `List<SettlementSummaryDto>`
- `GetDetailAsync(settlementId, callerId)` → `SettlementDetailDto` with `ApprovalStep` history
- `ConfirmSentAsync(settlementId, callerId)` → `SettlementDetailDto` — caller must be payer-family member; Proposed → PayerSent; creates `ApprovalStep(PayerSent)`
- `ConfirmReceivedAsync(settlementId, callerId)` → `SettlementDetailDto` — caller must be receiver-family member; PayerSent → Completed; creates `ApprovalStep(ReceiverConfirmed)`; when all settlements in activity are Completed, transitions Activity → Settled

**`BalanceCalculator`** — pure static balance logic (`Application/Core/BalanceCalculator.cs`)
- `Compute(expenses, participants)` → `Dictionary<Guid familyId, decimal balance>` — positive = creditor (owed money), negative = debtor (owes money)

**`SettlementOptimiser`** — pure static settlement optimiser (`Application/Core/SettlementOptimiser.cs`)
- `Optimise(balances)` → `List<Transfer>` — greedy min-transfer algorithm; at most N-1 transfers for N families

**`SplitCalculator`** — pure static split logic (`Application/Core/SplitCalculator.cs`)
- `CalculateShares(totalAmount, participants)` — distributes `totalAmount` by `WeightSnapshot` ratios; rounding remainder applied to the heaviest participant; excluded participants get 0

**`ActivityService`** — activity operations (any group member)
- `ListAsync(groupId, callerId)` → top-level activities for a group (no-parent)
- `GetDetailAsync(activityId, callerId)` → full detail with participants + sub-activities
- `CreateAsync(groupId, req, callerId)` → new top-level activity; seeds participants from all active group members via `ParticipantSeeder`
- `CreateSubActivityAsync(parentId, req, callerId)` → depth-1 guard; seeds from parent's participants
- `UpdateAsync(activityId, req, callerId)` → name/description; Open only
- `CloseAsync(activityId, callerId)` → closes activity; Open sub-activities → `AbsorbedByParent`
- `AddParticipantAsync(activityId, req, callerId)` → adds a group member; Open only
- `RemoveParticipantAsync(activityId, memberId, callerId)` → removes a participant; Open only

**`ParticipantSeeder`** — seeds `ActivityParticipant` rows
- `SeedForActivityAsync(activity)` → all active FamilyMembers of all families in the group
- `SeedForSubActivityAsync(sub, parentId)` → copies parent activity's participant list

### Key patterns

**Resolving caller's FamilyId:**
```csharp
var familyId = await _db.FamilyMembers
    .Where(m => m.UserId == userId && m.IsActive)
    .Select(m => (Guid?)m.FamilyId)
    .FirstOrDefaultAsync();
return familyId ?? throw Forbidden();
```

**EF Core no-tracking cycle detection:** EF Core 10 throws cycle errors when navigation properties are accessed in LINQ even inside `Select` projections. **Fix:** always use explicit `join ... on ... equals ...` from DbSet roots with flat scalar projections. Never access navigation properties in LINQ queries.

```csharp
// WRONG — triggers NavigationExpandingExpressionVisitor cycle:
_db.Groups.Select(g => new { Count = g.GroupFamilies.Count })...

// CORRECT — explicit join, scalar projection only:
from gf in _db.GroupFamilies
join f in _db.Families on gf.FamilyId equals f.Id
where gf.GroupId == groupId
select new { gf.FamilyId, f.Name, gf.Role, gf.JoinedAt }
```

**Validation:** All service entry points call `await validator.ValidateAndThrowAsync(req)`. FluentValidation validators are registered via `AddValidatorsFromAssembly`.

**Error types:**
- `ForbiddenException` → 403 (caller not in group/family, or wrong role)
- `ValidationException` → 422 (invalid input, business rule violations, not found)

---

## Infrastructure Layer (FamilySplit.Infrastructure)

- Single `AppDbContext` with entity configurations in `Configurations/`.
- SQLite in dev, PostgreSQL in production.
- Migrations in `Migrations/`.
- `FamilyMember.Email` has a **filtered unique index** (`WHERE email IS NOT NULL`).
- `GroupMembership` entity/config are empty stubs retained for git history — replaced by `GroupFamily`.

### DbSets

```csharp
DbSet<User>                Users
DbSet<Family>              Families
DbSet<FamilyMember>        FamilyMembers
DbSet<Group>               Groups
DbSet<GroupFamily>         GroupFamilies
DbSet<Activity>            Activities
DbSet<ActivityParticipant> ActivityParticipants
DbSet<Expense>             Expenses
DbSet<ExpenseParticipant>  ExpenseParticipants
DbSet<Settlement>          Settlements
DbSet<ApprovalStep>        ApprovalSteps
DbSet<Category>            Categories
DbSet<AuditLog>            AuditLogs
DbSet<RefreshToken>        RefreshTokens         // Phase 7: refresh-token rotation
DbSet<DataProtectionKey>   DataProtectionKeys    // ASP.NET DP key ring (encrypts PKCE cookie)
```

---

## Client Layer (FamilySplit.Client)

### Architecture
- Blazor WebAssembly SPA with MudBlazor 8 for UI.
- **Fluxor 6** (Redux-style state management):
  - `[FeatureState]` records define state slices.
  - `[ReducerMethod]` static methods on reducer classes.
  - `[EffectMethod]` methods on effect classes (must be explicitly registered as `AddScoped<TEffects>()`).
  - `<StoreInitializer />` in `App.razor` initialises the store.

### Refit clients (in `Services/`)

| Interface | Base path | Purpose |
|---|---|---|
| `IHealthApi` | `/health` | Anonymous health check |
| `IWhoAmIApi` | `/users/me` | Current User info |
| `IFamilyMemberClient` | `/users/me/profile` | Caller's FamilyMember profile |
| `IFamilyClient` | `/families/mine` | Own-family management |
| `IAdminClient` | `/admin/families` | Global-admin family management |
| `IGroupClient` | `/groups` | Group CRUD + join + invite code |
| `IActivityClient` | `/groups/{groupId}/activities` | Activity CRUD + sub-activities + participants + close |
| `IExpenseClient` | `/groups/{groupId}/activities/{activityId}/expenses` | Expense CRUD |
| `ISettlementClient` | `/groups/{groupId}/activities/{activityId}/settlements` | Settlement generation + approval flow |
| `IHandoffApi` | `/auth/handoff` | JWT retrieval (uses credentials) |

### Fluxor Stores

| Store | State | Key actions |
|---|---|---|
| `FamilyMemberState` | `MyProfile: FamilyMemberDto?` | `LoadMyProfileAction` |
| `FamilyState` | `MyFamily: FamilyDto?` | `LoadMyFamilyAction`, `UpdateFamilyNameAction`, `AddFamilyMemberAction`, `UpdateFamilyMemberAction`, `RemoveFamilyMemberAction` |
| `GroupState` | `Groups`, `SelectedGroup`, `IsLoading`, `ErrorMessage` | `LoadGroupsAction`, `LoadGroupDetailAction`, `CreateGroupAction`, `UpdateGroupAction`, `JoinGroupAction`, `RegenerateInviteCodeAction` |
| `ActivityState` | `Activities`, `SelectedActivity`, `IsLoading`, `ErrorMessage` | `LoadActivitiesAction`, `LoadActivityDetailAction`, `CreateActivityAction`, `CreateSubActivityAction`, `UpdateActivityAction`, `CloseActivityAction`, `AddParticipantAction`, `RemoveParticipantAction` |
| `ExpenseState` | `Expenses`, `SelectedExpense`, `IsLoading`, `ErrorMessage` | `LoadExpensesAction`, `LoadExpenseDetailAction`, `CreateExpenseAction`, `UpdateExpenseAction`, `DeleteExpenseAction`, `ClearExpensesAction` |
| `SettlementState` | `Settlements`, `Balances`, `SelectedSettlement`, `IsLoading`, `IsGenerating`, `ErrorMessage` | `LoadSettlementsAction`, `LoadBalancesAction`, `GenerateSettlementsAction`, `LoadSettlementDetailAction`, `ConfirmSentAction`, `ConfirmReceivedAction`, `ClearSettlementsAction` |

### Key pages

| Route | Page | Description |
|---|---|---|
| `/` | `Index.razor` | Dashboard / home |
| `/groups` | `GroupList.razor` | List of caller's groups (shows family count) |
| `/groups/{id}` | `GroupDetail.razor` | Group detail — families with nested members + activity list |
| `/groups/{groupId}/activities/{activityId}` | `ActivityDetail.razor` | Activity detail — participants, sub-activities, close/edit; settlements section on Closed/Settled activities |
| `/families/mine` (also `/profile`) | `ManageFamily.razor` | Own-family management — rename, add/edit/remove members |
| `/auth/return` | `AuthReturn.razor` | JWT handoff page |
| `/not-registered` | `NotRegistered.razor` | Shown when login email has no FamilyMember |

### Auth on the client
- JWT stored in `sessionStorage`.
- `JwtAuthHandler` (delegating handler) attaches `Authorization: Bearer <token>` to all authenticated Refit calls.
- `IncludeCredentialsHandler` sets `credentials: include` for the handoff cookie exchange.
- `AuthService` manages token storage and expiry.

---

## Development Notes

### Running locally

```bash
# API (runs on https://localhost:5081)
cd src/FamilySplit.Api
dotnet run

# Client (runs on https://localhost:5001)
cd src/FamilySplit.Client
dotnet run
```

### User secrets required (API)

```json
{
  "Jwt:SigningKey": "<at-least-32-chars>",
  "OAuth:Google:ClientId": "<from Google Cloud Console>",
  "OAuth:Google:ClientSecret": "<from Google Cloud Console>"
}
```

### DB reset required after Phase 3.5

The Initial migration was fully rewritten. Drop and recreate:
```bash
cd src/FamilySplit.Infrastructure
dotnet ef database drop --startup-project ../FamilySplit.Api --force
dotnet ef database update --startup-project ../FamilySplit.Api
```

Then seed manually:
```sql
-- Insert a family
INSERT INTO families (id, name, created_at, updated_at)
VALUES (gen_random_uuid(), 'My Family', now(), now());

-- Insert a family member (replace email and family_id)
INSERT INTO family_members (id, family_id, display_name, email, is_admin, is_active, created_at)
VALUES (gen_random_uuid(), '<family-id>', 'Your Name', 'you@example.com', true, true, now());
```

After first login, set global admin:
```sql
UPDATE users SET is_global_admin = true WHERE email = 'you@example.com';
```

### Phase 6 migration

Phase 6 refactored `Settlement` to link Families directly (instead of Users). Apply the migration:
```bash
cd src/FamilySplit.Infrastructure
dotnet ef database update --startup-project ../FamilySplit.Api
```
This runs `20260523000000_Phase6SettlementsRefactorToFamilies` which drops `payer_user_id`/`receiver_user_id` and adds `payer_family_id`/`receiver_family_id` on the `settlements` table.

### EF Core migrations

```bash
cd src/FamilySplit.Infrastructure
dotnet ef migrations add <Name> --startup-project ../FamilySplit.Api
dotnet ef database update --startup-project ../FamilySplit.Api
```

---

## UI Naming Conventions

Consistent terminology must be used across all pages, dialogs, buttons, and tooltips. Never mix these terms.

### Settlement / Transfer vocabulary

The backend entity is always called `Settlement` in code, DTOs, and Fluxor actions. In the **UI** the word used depends on context:

| Context | Term to use | Rationale |
|---|---|---|
| Home page (`Index.razor`) — pending items across all groups | **Settlements** | Cross-group, administrative view |
| Group detail page (`GroupDetail.razor`) — pending items for a group | **Settlements** | Group-level administrative view |
| Activity detail page (`ActivityDetail.razor`) — payment items for one activity | **Transfers** | Per-activity operational view |
| Dialog title when opening a settlement detail from ActivityDetail | **Transfer detail** | Matches page terminology |
| `Balance` section on ActivityDetail | **Balance** | Per-family net amounts (positive = to receive, negative = to pay) |

### Action button labels (canonical, site-wide)

These exact labels must appear on every button, in every confirmation dialog `yesText`, and in every tooltip — no variations allowed:

| Action | Label |
|---|---|
| Payer marks payment as sent | **Mark sent** |
| Receiver confirms payment received | **Mark received** |

Confirmation `ShowMessageBox` titles should be **"Mark as sent"** / **"Mark as received"** (descriptive phrase form). The `yesText` must match the button: `"Mark sent"` / `"Mark received"`.

### Status chip labels

`SettlementStatus` enum values are displayed in chips. Use these display strings:

| Enum value | Display label |
|---|---|
| `Proposed` | `Proposed` |
| `PayerSent` | `Sent` |
| `Completed` | `Completed` |
| `Cancelled` | `Cancelled` |

---

## Error Handling Conventions

### API error response shapes

The middleware produces two structured error bodies:

**403 Forbidden** (`ForbiddenException`):
```json
{ "type": "...", "title": "Forbidden", "status": 403, "detail": "reason" }
```

**422 Unprocessable Entity** (`ValidationException`):
```json
{ "type": "...", "title": "Validation failed", "status": 422, "errors": { "Field": ["msg"] } }
```

### Client error handling rules

1. **Never show raw HTTP status strings to users.** All Refit `ApiException`s must go through `ErrorHelper.GetMessage(ex)` (`Services/ErrorHelper.cs`), which parses the structured body above and falls back to a friendly status-code message.
2. **Always log the full exception** in Effects before dispatching the failure action, using the injected `ILogger<T>`.
3. **Every store feature** (Admin, Family, Groups, FamilyMembers) has a `Clear*ErrorAction` and a matching reducer that sets `ErrorMessage = null`. Effects dispatch this automatically — it is also dispatched from the UI when the user clicks the alert's close icon.
4. **Every dismissable `MudAlert`** must wire `CloseIconClicked` to dispatch the store's `Clear*ErrorAction`:

```razor
<MudAlert Severity="Severity.Error" ShowCloseIcon="true"
          CloseIconClicked="@(() => Dispatcher.Dispatch(new ClearFamilyErrorAction()))">
    @State.Value.ErrorMessage
</MudAlert>
```

5. **Soft-deleted records** (`IsActive = false`) must be excluded from every query. Always add `&& m.IsActive` when filtering `FamilyMembers`. Email uniqueness checks must also filter by `IsActive` so a deleted member's email can be reused.

### Mobile touch targets

FamilySplit is designed to be usable on mobile. All interactive controls must meet the minimum 48 dp touch target recommended by Material Design and Apple HIG.

**Rules (apply site-wide, not just per-page):**
- `MudIconButton` — always `Size.Medium` (≈40 px) as the minimum; use `Size.Large` (≈48 px) where space allows. **Never `Size.Small`** for icon-only buttons.
- `MudButton` (labelled) — `Size.Medium` or larger. `Size.Small` is acceptable only for buttons that appear inside dense data tables or compact chip rows where layout is constrained.
- Tooltips (`MudTooltip`) are hover-only and do not appear on touch screens — icon-only buttons must therefore be self-explanatory by icon, or carry a visible label on mobile layouts.
- Clickable `MudPaper` / card rows — no minimum size rule, but ensure `padding` provides at least 12 px vertical breathing room so the tap area is comfortable.

### UI permission guardrails

Client-side controls must mirror every server-side permission check — if a user can't do it, the control must be hidden. Key rules:
- `isAdmin` on ManageFamily/AdminFamilyDetail must be derived from the **current user's own member record** via `ProfileState.Value.MyProfile?.Id`, not from any member in the family.
- The logged-in user's remove-member button is always hidden (both ManageFamily and AdminFamilyDetail).
- The IsAdmin checkbox in `FamilyMemberDialog` is only shown when `ShowAdminToggle = true` (pass `callerIsAdmin`).
- Admin pages (`/admin/*`) dispatch `LoadMyProfileAction` on init if `ProfileState.Value.MyProfile is null`, since they need the caller's identity for UI guards.
- Use **computed C# properties** in `@code` (not `@{ }` template variables) for values that depend on async state — template variables are captured at first render and won't update when Fluxor state changes.

---

## Logging Standards

All service methods **must** follow these rules. They apply to every new service and must be maintained when modifying existing ones.

### Log levels — when to use what

| Level | Use for |
|---|---|
| `LogDebug` | Method entry — every public service method. Include the primary entity IDs and the caller's `UserId`. Never enable in production by default; flip `FamilySplit` override to `Debug` via env var to investigate without a redeploy. |
| `LogInformation` | Successful mutations — create, update, delete, state transitions. Log the new/changed entity ID plus `{UserId}`. Security events (group join/leave, token revocation) also belong here. |
| `LogWarning` | Elevated-privilege or destructive actions (global-admin deletes, invite-code regeneration, mass token revocation triggered by logout). Also used for detected anomalies (replay/theft attempts). |
| `LogError` | Unexpected exceptions not handled by business logic. The global `ValidationExceptionMiddleware` already catches FluentValidation and ForbiddenException — do **not** catch those just to log them; the middleware handles it. |

`LogTrace` is not used in this codebase.

### Structured logging rules

- **Always use named placeholders** — `{ExpenseId}`, `{UserId}`, `{Amount}` — never C# string interpolation inside the message template. Serilog serialises placeholders as structured properties.
- **Never log PII** at `Debug` or below. Display names, email addresses, and raw amounts may appear in `Information`-level mutation logs (they record what happened) but must not appear in `Debug` entry logs.
- **Property naming convention** — entity ID placeholders must be `{EntityType}Id` (e.g., `{GroupId}`, `{ActivityId}`, `{ExpenseId}`). The caller is always `{UserId}`. Counts use `{Count}`.

### Constructor injection pattern

```csharp
// CORRECT — logger is the last constructor parameter
public class MyService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MyService> _logger;

    public MyService(AppDbContext db, ILogger<MyService> logger)
    {
        _db     = db;
        _logger = logger;
    }
}
```

### Standard entry / exit log pattern

```csharp
public async Task<Foo> CreateAsync(Guid groupId, CreateFooRequest req, Guid callerId)
{
    // Debug entry — key IDs only, no PII
    _logger.LogDebug("Creating foo in group {GroupId} by user {UserId}", groupId, callerId);

    // ... business logic ...

    await _db.SaveChangesAsync();

    // Info on success — entity ID + caller
    _logger.LogInformation("Foo {FooId} created in group {GroupId} by user {UserId}",
        foo.Id, groupId, callerId);

    return dto;
}
```

### Audit logging (financial mutations only)

Expense and settlement mutations are additionally persisted to the `audit_log` table via `AuditService.Queue(userId, entityType, entityId, action, metadata)`. The entry is saved atomically inside the same `SaveChangesAsync` call — do not call a separate `SaveChangesAsync` for audits. Supported entity types: `Expense`, `Settlement`. Supported actions: `Created`, `Updated`, `Deleted`, `Generated`, `ConfirmSent`, `ConfirmReceived`. The `metadata` field is a JSONB blob with a before/after diff or creation snapshot.

Non-financial mutations (group join/leave, member changes, family renames) are covered by Serilog `LogInformation` only — they do not write to the `audit_log` table.

### appsettings configuration

| Environment | `FamilySplit.*` level | How to investigate in prod |
|---|---|---|
| Production (`appsettings.json`) | `Information` | Set env var `Serilog__MinimumLevel__Override__FamilySplit=Debug` without redeploying |
| Development (`appsettings.Development.json`) | `Debug` | Default — all entry logs visible |

Microsoft/System namespaces stay at `Warning` in both environments. EF SQL commands (`Microsoft.EntityFrameworkCore.Database.Command`) are `Information` in dev only.

---

## Phase Roadmap

| Phase | Status | Scope |
|---|---|---|
| 1 | ✅ Complete | Auth (OAuth/PKCE/JWT), User entity, WhoAmI |
| 2 | ✅ Complete | FamilyMember entity, WeightCalculator, profile endpoint |
| 3 | ✅ Complete | Groups (CRUD, invite codes), FamilyMember↔User link |
| 3.5 | ✅ Complete | Family entity, GlobalAdmin, GroupFamily replaces GroupMembership; AdminService + FamilyService; Family management UI |
| 4 | ✅ Complete | Activities (top-level + sub-activities depth-1, participant management, close flow absorbs open subs) |
| 5 | ✅ Complete | Expenses (attach to activity, weight snapshots, SplitCalculator, per-participant breakdown UI) |
| 6 | ✅ Complete | Settlements (BalanceCalculator, SettlementOptimiser, ConfirmSent/ConfirmReceived approval flow, Activity→Settled transition, balance + settlement UI in ActivityDetail) |
