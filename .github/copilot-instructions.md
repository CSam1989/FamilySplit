# FamilySplit — GitHub Copilot Instructions

> These instructions are automatically picked up by GitHub Copilot in this repository.
> For the full architecture reference, see [`CLAUDE.md`](../CLAUDE.md).

---

## Tech stack

.NET 10 · Blazor WebAssembly · ASP.NET Core Minimal API · PostgreSQL · Entity Framework Core 10 · Serilog · MudBlazor 8 · Fluxor 6 · Refit 8 · FluentValidation 11

---

## Logging standards (enforce on every suggestion)

### Log levels

| Level | When |
|---|---|
| `LogDebug` | **Every** public service method entry — key entity IDs + `{UserId}` only |
| `LogInformation` | Successful mutations (create / update / delete / state-transition) and security events (join, leave, token revoke) |
| `LogWarning` | Destructive or elevated-privilege operations (global-admin deletes, invite-code regeneration, mass-revocation) and detected anomalies |
| `LogError` | Unexpected exceptions not covered by global middleware |

`LogTrace` is not used. `LogDebug` is disabled in production by default — flip via env var `Serilog__MinimumLevel__Override__FamilySplit=Debug`.

### Structured logging — always use named placeholders

```csharp
// ✅ CORRECT — structured property
_logger.LogInformation("Expense {ExpenseId} created on activity {ActivityId} by user {UserId}",
    expense.Id, activityId, callerId);

// ❌ WRONG — string interpolation destroys structured properties
_logger.LogInformation($"Expense {expense.Id} created by {callerId}");
```

### Placeholder naming convention

- Entity IDs → `{EntityType}Id` e.g. `{GroupId}`, `{ActivityId}`, `{ExpenseId}`, `{SettlementId}`
- Authenticated caller → `{UserId}` (always — it is the `User.Id` Guid from the JWT `sub` claim)
- Counts → `{Count}`
- Never log PII (display names, email addresses) in `LogDebug` entry logs

### Constructor injection — logger is always the last parameter

```csharp
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

### Standard entry + success pattern

```csharp
public async Task<FooDto> CreateFooAsync(Guid groupId, CreateFooRequest req, Guid callerId)
{
    _logger.LogDebug("Creating foo in group {GroupId} by user {UserId}", groupId, callerId);

    // ... validation, auth checks, business logic ...

    await _db.SaveChangesAsync();

    _logger.LogInformation("Foo {FooId} created in group {GroupId} by user {UserId}",
        foo.Id, groupId, callerId);

    return dto;
}
```

---

## Audit logging (financial mutations only)

Expense and Settlement mutations must also be recorded in the `audit_log` table via `AuditService`:

```csharp
// Queue the entry BEFORE SaveChangesAsync — it is persisted atomically
_audit.Queue(callerId, "Expense", expense.Id, "Created", new
{
    activityId,
    title  = expense.Title,
    amount = expense.TotalAmount,
    currency = expense.Currency,
});

await _db.SaveChangesAsync();
```

- `AuditService` is scoped — it shares the same `AppDbContext` as the calling service
- Supported entity types: `Expense`, `Settlement`
- Supported actions: `Created` | `Updated` | `Deleted` | `Generated` | `ConfirmSent` | `ConfirmReceived`
- Non-financial mutations (groups, members, families) use `LogInformation` only — no audit table entry

---

## Shared Blazor components — use before writing new markup

All shared components live in `src/FamilySplit.Client/Components/Shared/` and are globally available via `_Imports.razor`. Before writing any repeated markup pattern, check whether one of these components fits:

| Component | Use when |
|---|---|
| `<PageErrorBanner ErrorMessage="..." OnDismiss="..." />` | Any dismissable store error alert (replaces inline `MudAlert` + `ShowCloseIcon`) |
| `<EmptyState Icon="..." Title="..." Subtitle="..." />` | Any dashed-border empty-state card (replaces inline `MudPaper` + `MudIcon` + `MudText`) |
| `<SectionHeader Title="..."><Actions>...</Actions></SectionHeader>` | Any `h6` title row with a right-aligned action button |
| `<StatCard Icon="..." IconColor="..." Value="..." Label="..." />` | Any centred stat tile (icon + large value + caption) |
| `<GroupStatsChips Stat="..." IsLoading="..." />` | Group activity/spend/balance/pending chip row on group cards |
| `<MemberRoleChip IsAdmin="..." />` | Admin / Member role chip in a member table |
| `<MemberStatusChip IsLinked="..." HasEmail="..." />` | Linked / Pending account-link chip in a member table |
| `<SettlementRow SettlementId="..." PayerFamilyId="..." PayerFamilyName="..." ReceiverFamilyId="..." ReceiverFamilyName="..." Amount="..." Currency="..." Status="..." CallerFamilyId="..." OnMarkSent="..." OnMarkReceived="..."><TrailingActions>...</TrailingActions></SettlementRow>` | Payer→receiver row with amount, status chip, optional Mark sent/received buttons, and a trailing-actions slot |
| `<MemberActionCell MemberId="..." CallerId="..." ShowRemove="..." OnEdit="..." OnRemove="..." />` | Edit + Remove icon-button pair for a member table row; hides Remove when member is the caller |

### Helper — `FormatHelper` (static, no inject needed)

```csharp
FormatHelper.FormatAmount(decimal amount, string currency)  // → "€ 12.50"
FormatHelper.AvatarColor(string name)                       // → deterministic hex colour
```

Never duplicate these as private static methods inside a page or component.

### Adding a new shared component

1. Create the `.razor` file in `Components/Shared/`.
2. Use `[Parameter, EditorRequired]` for required inputs.
3. Inject `I18nText` and load `AppText` if the component renders any user-visible strings.
4. Document the component in this table and in `CLAUDE.md`.

---

## Internationalisation — hardcoded strings are forbidden in the client

**Never write a raw string literal as visible text in a `.razor` file.** Every user-facing string must come from the `AppText` i18n table.

### Pattern (required in every razor file that shows text)

```razor
@inject I18nText I18nText

private AppText _t = new();

protected override async Task OnInitializedAsync()
{
    await base.OnInitializedAsync(); // must come first for FluxorComponent subclasses
    _t = await I18nText.GetTextTableAsync<AppText>(this);
}
```

### Adding new strings

1. Add the key to **all four** JSON files in `src/FamilySplit.Client/i18ntext/`:
   - `AppText.en.json` — English (default)
   - `AppText.nl.json` — Dutch
   - `AppText.fr.json` — French
   - `AppText.de.json` — German
2. Reference it in markup as `@_t.YourNewKey`.
3. Missing keys in any JSON file fall back to the key name — always provide all four translations.

This rule applies to: labels, button text, dialog titles, confirmation messages, tooltips, chip labels, placeholder text, error messages, and empty-state copy.

---

## Testing

**Every new feature, endpoint, validator, component, or user flow must ship with tests. CI gates on all four suites.**

### Test projects (correct names — do not invent others)

| Project | Type | Stack |
|---|---|---|
| `FamilySplit.UnitTests` | Unit — server pure logic | xUnit, FluentAssertions, **Moq** |
| `FamilySplit.Client.UnitTests` | Unit — Blazor bUnit | xUnit, bUnit, FluentAssertions, **Moq** |
| `FamilySplit.IntegrationTests` | Integration | xUnit, Testcontainers (PostgreSQL), `WebApplicationFactory`, Npgsql |
| `FamilySplit.E2ETests` | End-to-end | xUnit, Playwright, Testcontainers (PostgreSQL) |

**Mocking library: Moq.** Never NSubstitute.

### What to add per change

| Change | Where to add tests |
|---|---|
| New service method / business rule | `FamilySplit.UnitTests` (if pure logic) + `FamilySplit.IntegrationTests` (endpoint) |
| New validator | `FamilySplit.UnitTests` — one test per rule + happy path |
| New shared Blazor component | `FamilySplit.Client.UnitTests` — bUnit render tests per prop variant |
| New page with permission guards | `FamilySplit.Client.UnitTests` — bUnit test with mocked Fluxor state |
| New user flow | `FamilySplit.E2ETests` — Playwright flow test |
| New interactive UI element | Add `data-testid` attribute; use it in the E2E test immediately |

### Unit tests — server (`FamilySplit.UnitTests`)

Cover calculators, validators, business guards — **no DB, no HTTP**.

```csharp
public class CreateActivityValidatorTests
{
    private readonly CreateActivityValidator _sut = new();

    [Fact]
    public async Task Name_empty_fails()
    {
        var result = await _sut.TestValidateAsync(new CreateActivityRequest { Name = "" });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
```

### Client unit tests — bUnit (`FamilySplit.Client.UnitTests`)

**Always derive from `BunitTestContext`** (in `Infrastructure/`) — it pre-registers MudBlazor services, a stubbed `I18nText` (returns default `AppText`), and no-op Fluxor infrastructure.

```csharp
// ✅ Correct base class
public class SettlementRowTests : BunitTestContext
{
    [Fact]
    public void MarkSent_Button_Shown_To_Payer_When_Proposed()
    {
        var cut = RenderComponent<SettlementRow>(p => p
            /* ... required props ... */
            .Add(x => x.OnMarkSent, EventCallback.Empty));

        cut.Find("[data-testid='btn-mark-sent-...']").Should().NotBeNull();
    }
}
```

For pages with `@inherits FluxorComponent`, register `Mock<IState<TState>>()` + `Mock<IDispatcher>()` + `Mock<IState<AuthState>>()` (with `IsAuthenticated = true`) via `Services.AddSingleton(...)`.

Do NOT add `global using Bunit;` — it conflicts with xUnit v3's `TestContext`. Add `using Bunit;` locally per file.

### Integration tests (`FamilySplit.IntegrationTests`)

Derive from `IntegrationTestBase` (opens Testcontainers Postgres, starts API in-process via `CustomWebApplicationFactory`, seeds a caller user, mints a JWT). Every test method runs inside a transaction that rolls back on completion.

```csharp
[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class MyEndpointTests : IntegrationTestBase
{
    public MyEndpointTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task HappyPath_Returns201()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.PostAsync("/my-endpoint", JsonContent.Create(new { ... }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

Cover per feature: happy-path CRUD · 403 permission boundaries · 422 validation rejection · state transitions.

### E2E tests (`FamilySplit.E2ETests`)

Derive from `E2ETestBase`, belong to `[Collection(nameof(E2ECollection))]`. Call `AuthenticateContextAsync()` before navigating to set the refresh-token cookie. Use `data-testid` selectors — **never text content or CSS classes**.

```csharp
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class MyFlowTests : E2ETestBase
{
    public MyFlowTests(E2EApiServer api, E2EClientServer client) : base(api, client) { }

    [Fact]
    public async Task MyFlow_HappyPath()
    {
        if (!ClientAvailable) return;
        await AuthenticateContextAsync();
        await Page.GotoAsync("/my-page");
        await Page.ClickAsync("[data-testid='btn-my-action']");
        await Expect(Page.Locator("[data-testid='result-element']")).ToBeVisibleAsync();
    }
}
```

**data-testid convention:** `btn-{action}` for buttons, `{noun}-row-{id}` for table rows, `{noun}-status-{id}` for status chips. Always include the entity `Guid` when there can be multiple instances.

### General rules

- Arrange-Act-Assert. One concern per test.
- Names: `Subject_Condition_ExpectedOutcome`
- No `Thread.Sleep` — use Playwright's `Expect(...).ToBeVisibleAsync()`
- Each test seeds its own data; never assumes pre-existing rows
- Tag slow tests: `[Trait("Category", "Integration")]` / `[Trait("Category", "E2E")]`

---

## Other conventions to follow

### EF Core — never use navigation properties in LINQ

```csharp
// ❌ WRONG — triggers NavigationExpandingExpressionVisitor cycle in EF Core 10
_db.Groups.Select(g => new { Count = g.GroupFamilies.Count })

// ✅ CORRECT — explicit join, scalar projection only
from gf in _db.GroupFamilies
join f in _db.Families on gf.FamilyId equals f.Id
where gf.GroupId == groupId
select new { gf.FamilyId, f.Name }
```

### Errors

- Business rule violations → `FluentValidation.ValidationException` → caught by `ValidationExceptionMiddleware` → HTTP 422
- Auth/permission failures → `ForbiddenException` → caught by `ValidationExceptionMiddleware` → HTTP 403
- Do **not** add try/catch to service methods just to log; the middleware handles it

### Soft deletes

`FamilyMember.IsActive = false` — always filter `&& m.IsActive` in every query touching `FamilyMembers`.

### Caller identity

All service methods receive `callerId` as `Guid` (the `User.Id` from the JWT `sub` claim). Resolve family via:

```csharp
var familyId = await _db.FamilyMembers
    .Where(m => m.UserId == callerId && m.IsActive)
    .Select(m => (Guid?)m.FamilyId)
    .FirstOrDefaultAsync()
    ?? throw new ForbiddenException();
```
