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
