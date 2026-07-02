# Vertical Slice Architecture Refactor — Phased Plan

> Execution tracker for restructuring the FamilySplit backend into vertical slices with a
> CQRS split inside each slice. Each phase is one green commit: `dotnet build FamilySplit.slnx -c Release`
> + UnitTests + IntegrationTests (+ Client.UnitTests for phases that touch the client) must pass
> before committing. Phases are designed to be executed independently (one per session is fine) —
> state is always releasable between phases.
>
> Branch: `refactor/vertical-slices`. Baseline (commit `7034697`): 688 unit / 431 client / 96 integration tests green.
>
> **Revision (2026-06-16) — data-access seam.** The CQRS split is taken one step further: the
> *business logic* (commands) and the *data access* (queries) are split into separately-testable
> layers. Command handlers no longer touch `AppDbContext` — they depend on a mockable per-slice
> data interface `I{Slice}Data`, so business logic is unit-tested with **Moq** (no DB). All EF
> access lives in query handlers and the `{Slice}Data` implementation, which are verified against a
> real Postgres with **Testcontainers**. See [ADR-001](#adr-001--business-logic--data-access-seam-binding)
> and the revised [Decision 3](#decisions-binding-user-confirmed) below. This reshapes the already-merged
> Expenses + Dashboard slices — see the Retrofit callouts in their phase entries.

---

## Decisions (binding, user-confirmed)

1. **One class-library project per feature slice** under `src/Features/`, grouped in a `/src/Features/` solution folder. Thin API host keeps the name `FamilySplit.Api` (E2E `dotnet run` + Dockerfile depend on it).
2. **`FamilySplit.Domain` + `FamilySplit.Infrastructure` stay unchanged** (entities, enums, AppDbContext, EF configs, migrations).
3. **Use-case folders inside each slice, split CQRS-style** (e.g. `Expenses/Create/`, `Expenses/GetDetail/`). Service classes dissolve into one handler per use case. **No MediatR.** Each slice has **two physically-separable layers — business logic and data access** (ADR-001):
   - **Queries** (reads) **are** the data-access layer: thin handlers doing EF projections — `AsNoTracking()`, no mutations, no business rules (authorization guards still apply). They may use `AppDbContext` directly and are **Testcontainers-tested** (the read-side wire-format lock).
   - **Commands** (writes) **are the business logic** — and contain **no EF**. They depend on a per-slice **`I{Slice}Data`** interface (the mockable data seam) plus pure calculators/guard interfaces, and run: validation → guards → rules → `data.…` calls. A command handler **must not** reference `AppDbContext`, `DbSet<>`, or call `SaveChangesAsync`. They are **unit-tested with Moq** (mock the data, no DB).
   - **`{Slice}Data : I{Slice}Data`** (in the slice's `Data/` folder) is the only write-side code that touches EF: the reads a command needs + the persistence (`Add`/`Update`/`Remove` + `SaveChangesAsync`, audit flush). `internal sealed`, registered scoped, **Testcontainers-tested**.
4. **Strict CQRS command responses (BREAKING wire-format change, chosen deliberately):** commands no longer return detail DTOs. Creates return `201 Created` + `Location` + `{ "id": "<guid>" }`; all other mutations return `204 No Content`. The client re-queries after mutating. See the response-semantics table below for the per-endpoint mapping and the two documented exceptions (Join, RegenerateInviteCode).
5. **Per-slice DTOs and logic.** Only *truly shared* code goes in `FamilySplit.Common`. Slice-local: `SplitCalculator`+`ExpenseReshuffleRequired`→Expenses; `BalanceCalculator`+`SettlementOptimiser`+`SettlementStateMachine`→Settlements; `ParticipantSeeder`+`ActivityCloseGuard`→Activities. Common: `WeightCalculator` (4 slices), `AuditService` (mechanism only — slices own what they audit), `ForbiddenException`, `CreatedResponse`.
6. **Consistency enforcement:** `IFeatureModule` pattern + NetArchTest architecture tests (including CQRS rules).
7. **Keep the 4 test projects** (CI paths untouched); folders mirror slices.
8. **Blazor Client scope (revised by decision 4):** the client's internal architecture stays out of scope, but each slice phase must update the client's Refit mutation signatures, Fluxor effects (re-query after mutate), affected client DTOs, and their client unit tests — otherwise the app breaks against the new wire format.
9. **Business logic and data access are separate, separately-testable layers (ADR-001).** Command handlers are pure business logic over a mockable `I{Slice}Data` seam (unit-tested with Moq); query handlers and `{Slice}Data` are the only EF-touching code (Testcontainers-tested). No `AppDbContext` in command handlers. This is the binding refinement of decision 3 — see ADR-001.

### ADR-001 — Business logic / data-access seam (binding)

**Status:** Accepted (2026-06-16). **Supersedes** the original "commands hold business logic *and* `SaveChangesAsync`; handlers inject `AppDbContext`" wording of decision 3.

**Context.** With commands injecting `AppDbContext` directly, business logic could only be exercised against a database (InMemory provider in unit tests) — the rules and the EF queries were entangled in one class. We want (a) to verify *queries* against a real Postgres (Testcontainers, so EF-Core-10 translation quirks are caught), and (b) to unit-test *business logic* fast by **mocking the data** rather than spinning up a provider.

**Decision.** Split **every** slice into two layers — and **enforce the split by NetArchTest on every feature assembly, with no per-slice opt-out** (Rules 5, 8–11). The rule is universal: no business-logic class anywhere in `src/Features/` may hold an `AppDbContext`; all EF lives in query handlers or `{Slice}Data` gateways. (The legacy `FamilySplit.Application` services are exempt only because they are being deleted slice-by-slice — once a slice migrates, its code is bound by the rules.)

| Layer | What it is | EF? | Where | How it's tested |
|---|---|---|---|---|
| **Data access** | Read **query handlers** (`*QueryHandler`) + the write-side gateway **`{Slice}Data : I{Slice}Data`** | **Yes** — the only EF in the slice | `Data/` (gateway) + each query's use-case folder | **Testcontainers** (real Postgres) in `IntegrationTests` |
| **Business logic** | **Command handlers** (`*CommandHandler`) + pure calculators/guards | **No** | each command's use-case folder + `Shared/` | **Unit tests + Moq** over `I{Slice}Data` (no DB) in `UnitTests` |

- `I{Slice}Data` exposes (1) **reads** the command needs, returning plain DTOs/records (never tracked entities — so the seam stays mockable and tracking never leaks into business logic) and (2) **writes** that accept the new/changed state and persist it (encapsulating `Add`/`Update`/`Remove` + `SaveChangesAsync`, and the atomic **audit** flush for financial mutations).
- DB-touching **authorization guards** used by command handlers are exposed as **interfaces** (`IGroupMembershipGuard` in Common) so they too are mockable. Query handlers may keep the concrete guard.
- Pure calculators (`SplitCalculator`, `BalanceCalculator`, …) are unchanged — they take plain data, so command-handler unit tests call them for real (no mock needed).

**Consequences.**
- Rule 5 tightens and Rules 8–11 are added (see *Architecture rules* below); `DoesNotCallSaveChangesRule` now also covers command handlers.
- The InMemory-`DbContext` `{Slice}TestBase` for command tests is **removed**; command tests mock `I{Slice}Data`. Data-access tests move to Testcontainers.
- The already-merged **Expenses** and **Dashboard** slices must be retrofitted (see their phase entries).
- Trade-off accepted: one extra interface + impl per slice with commands, and write methods are coarser-grained (per-use-case persist methods) than raw `DbSet` calls.

## Verified facts

- `Application/FamilyMembers/*` are tombstones; `GET /users/me/profile` is backed by `FamilyService` → **no FamilyMembers slice**, Families absorbs it. `GroupMembersEndpoints` is a no-op stub → delete.
- `Api/Middleware/ForbiddenException.cs` is blank; real one is `Application/Exceptions/ForbiddenException.cs`.
- `RequireGroupMemberAsync` is duplicated **verbatim** in ActivityService:367, ExpenseService:388, SettlementService:533; caller-family lookup also in DashboardService and SettlementService:548 → `GroupMembershipGuard` in Common. **Per ADR-001 it is exposed as `IGroupMembershipGuard`** (Common) so command handlers can mock it; the concrete impl (uses `AppDbContext`, Testcontainers-tested) is registered scoped. NotificationHub keeps its inline non-throwing lookup (silent no-op, not 403 — do not change).
- `NotFound()`/`Throw422()` private helpers duplicated in 6 services → `ValidationErrors.NotFound/Field` in Common. **422-for-not-found semantics must be preserved** (client + integration tests depend on it).
- IntegrationTests contain **zero** `using FamilySplit.Application` (raw JSON asserts). **Query endpoints**: integration tests stay unmodified — they are the read-side wire-format lock. **Mutation endpoints**: tests are deliberately updated once per slice to assert the new 201/204 + follow-up-GET shape.
- `AdminService` reuses Families DTOs/validators → **Admin migrates before Families**.
- `ClaimsPrincipalExtensions` currently lives in the **global namespace** at the bottom of `Program.cs`; all endpoint files call `ctx.User.GetUserId()` with no using.

---

## Conventions (apply in every slice phase)

### Slice project csproj template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>FamilySplit.Features.{Name}</RootNamespace>
    <AssemblyName>FamilySplit.Features.{Name}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <!-- only if the slice has validators: -->
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\FamilySplit.Common\FamilySplit.Common.csproj" />
    <ProjectReference Include="..\..\FamilySplit.Domain\FamilySplit.Domain.csproj" />
    <ProjectReference Include="..\..\FamilySplit.Infrastructure\FamilySplit.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="FamilySplit.UnitTests" />
    <InternalsVisibleTo Include="FamilySplit.IntegrationTests" /> <!-- Testcontainers tests resolve the internal {Slice}Data -->
    <Using Include="FamilySplit.Common.Security" /> <!-- GetUserId() without per-file usings -->
  </ItemGroup>
</Project>
```

> `I{Slice}Data` is **public** (command handlers in other use-case folders consume it); `{Slice}Data` is **internal sealed** (only DI + Testcontainers tests touch it).

### CQRS classification and naming

| | Query use case (data access) | Command use case (business logic) |
|---|---|---|
| Purpose | Pure data access (the "access layer") | Business logic — **no EF** |
| Class names | `{UseCase}Query` (record, only if there are non-route inputs) + `{UseCase}QueryHandler` | `{UseCase}Command` (record, only if there is a request body) + `{UseCase}CommandHandler` |
| EF usage | `AsNoTracking()` projections; **never** `SaveChangesAsync`, never entity mutation | **None.** All EF goes through `I{Slice}Data`; never `AppDbContext`/`DbSet<>`/`SaveChangesAsync` |
| Allowed deps | `AppDbContext`, `IGroupMembershipGuard` (authz is not business logic), mappers | `I{Slice}Data`, validators, `IGroupMembershipGuard`, slice calculators/guards (audit + notify flow **through** `I{Slice}Data`'s persist) |
| Validation | None today (route-bound ids only) | `await _validator.ValidateAndThrowAsync(cmd, ct)` first statement when a body exists |
| Returns | DTOs (same shapes as today — read side is the wire-format lock) | `Guid` id for creates, nothing for everything else |
| Tested by | **Testcontainers** (real Postgres) | **Moq** over `I{Slice}Data` (no DB) |

The **`{Slice}Data` gateway** (the third class) lives in `Data/`, owns the tracked entities + `SaveChangesAsync`, exposes reads as plain records and writes as per-use-case persist methods, and is Testcontainers-tested alongside the queries.

- Validators are named `{UseCase}CommandValidator` and live in the command's folder (they validate the `{UseCase}Command` record).
- Route-bound parameters (groupId, activityId, callerId) pass directly into `HandleAsync(...)` — don't wrap them in records.
- Response DTOs live **in the query's use-case folder** (e.g. `GetDetail/ExpenseDetailDto.cs`); they move to slice `Shared/` only when a second use case in the same slice needs them (e.g. `ExpenseParticipantDto` used by List + GetDetail).
- Because commands no longer return detail DTOs, the old service `BuildDetailDtoAsync` helpers collapse **into the GetDetail query handler** — most planned `Shared/*Reader` classes disappear.

### Business-logic / data-access seam — `I{Slice}Data` (ADR-001)

Every slice that has **commands** declares one data interface + one implementation. Slices with only queries (e.g. Dashboard) have **no** `I{Slice}Data` — their query handlers *are* the data access.

```
FamilySplit.Features.{Slice}/
├── {Query}/                       # READ — data access
│   ├── {Query}QueryHandler.cs        #   uses AppDbContext (AsNoTracking); Testcontainers-tested
│   ├── {Query}Endpoint.cs
│   └── {Query}Dto.cs
├── {Command}/                     # WRITE — business logic
│   ├── {Command}Command.cs
│   ├── {Command}CommandValidator.cs
│   ├── {Command}CommandHandler.cs    #   NO EF; depends on I{Slice}Data; Moq-tested
│   └── {Command}Endpoint.cs
├── Data/                          # WRITE-side data access (the mockable seam)
│   ├── I{Slice}Data.cs               #   public interface — reads (plain records) + persist methods
│   └── {Slice}Data.cs                #   internal sealed; AppDbContext + SaveChanges + audit; Testcontainers-tested
├── Shared/                        # pure calculators, slice DTOs (no EF)
└── {Slice}Module.cs
```

**Interface shape** — reads return plain records (never tracked entities); writes take state and persist atomically (incl. the audit entry for financial mutations):

```csharp
public interface IExpenseData
{
    // ── reads the commands need (plain records, no tracking leaks) ──
    Task<ActivityForExpense?> GetActivityAsync(Guid activityId, CancellationToken ct);
    Task<IReadOnlyList<ParticipantSnapshotInput>> GetActivityParticipantsAsync(Guid activityId, CancellationToken ct);
    Task<bool> CurrencyIsConsistentAsync(Guid activityId, string currency, Guid? excludeExpenseId, CancellationToken ct);
    Task<bool> CategoryIsValidAsync(Guid? categoryId, Guid groupId, CancellationToken ct);
    Task<ExpenseForUpdate?> GetExpenseForUpdateAsync(Guid expenseId, CancellationToken ct);

    // ── writes: accept the computed state, persist + flush audit atomically ──
    Task PersistNewExpenseAsync(Expense expense, IReadOnlyList<ExpenseParticipant> participants, AuditEntry audit, CancellationToken ct);
    Task PersistExpenseUpdateAsync(/* changed fields + recomputed participants */ AuditEntry audit, CancellationToken ct);
    Task DeleteExpenseAsync(Guid expenseId, AuditEntry audit, CancellationToken ct);
}
```

**Rules for the seam:**
- The command handler builds entities/values and calls `data.Persist…Async(…)`. It **never** sees a `DbSet`, an `IQueryable`, or a tracked entity. Calculators (`SplitCalculator`, weight snapshots) run **in the handler** on the plain records returned by the reads.
- `{Slice}Data` is the **only** place `SaveChangesAsync` is called on the write side, and the **only** place `AuditService.Queue` is flushed — so the audit write stays atomic with the mutation (CLAUDE.md "saved atomically inside the same `SaveChangesAsync`").
- Authorization stays a guard, but command handlers depend on **`IGroupMembershipGuard`** (mockable), not the concrete guard.
- DI: `services.AddScoped<IExpenseData, ExpenseData>();` in the module.

### Command response semantics (strict CQRS — the wire-format change)

| Mutation kind | HTTP response | Body |
|---|---|---|
| Create (expense, activity, sub-activity, group, family, member, …) | `201 Created` + `Location` header | `{ "id": "<guid>" }` (`CreatedResponse` from Common) |
| Update / rename / edit | `204 No Content` | — |
| Delete / remove / leave | `204 No Content` | — |
| State transition (close activity, confirm-sent, confirm-received) | `204 No Content` | — |
| Generate settlements | `204 No Content` (idempotent; client re-queries the list) | — |
| **Join group (exception)** | `200 OK` | `{ "id": "<groupId>" }` — the client only has the invite code and needs the id to navigate |
| **Regenerate invite code (exception path)** | `204 No Content` *if* `GroupDetailDto` already exposes the invite code (client re-queries detail); otherwise `200` + `{ "inviteCode": "..." }` — verify during Phase 6 |

Auth endpoints (`/auth/*`) are a token-exchange protocol, not domain CQRS — unchanged.

### Module shape

```csharp
public sealed class ExpensesModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(ExpensesModule).Assembly);
        services.AddScoped<IExpenseData, ExpenseData>();     // the data seam (ADR-001)
        services.AddScoped<ListExpensesQueryHandler>();      // one explicit line per handler
        services.AddScoped<GetExpenseDetailQueryHandler>();
        services.AddScoped<CreateExpenseCommandHandler>();
        // ...
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints.MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/expenses")
                           .WithTags("Expenses");
        ListExpensesEndpoint.Map(grp);
        CreateExpenseEndpoint.Map(grp);
        // ...
    }
}
```

Host `Program.cs` holds an **explicit module array** (compile-time checked; no reflection):

```csharp
IFeatureModule[] modules = [ new ExpensesModule(), /* grows per phase */ ];
foreach (var m in modules) m.RegisterServices(builder.Services, builder.Configuration);
// ... after builder.Build():
foreach (var m in modules) m.MapEndpoints(app);
```

### Handler shapes

```csharp
// QUERY — data access; uses AppDbContext directly (Testcontainers-tested)
public sealed class GetExpenseDetailQueryHandler
{
    // ctor-inject: AppDbContext, IGroupMembershipGuard, ILogger<T>
    public async Task<ExpenseDetailDto> HandleAsync(Guid expenseId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Fetching expense {ExpenseId} for user {UserId}", expenseId, callerId);
        // authz guard, then AsNoTracking projection (body absorbed from BuildDetailDtoAsync);
        // ValidationErrors.NotFound(...) when missing (422 semantics preserved)
    }
}

// COMMAND — business logic; NO EF. Reads + writes go through IExpenseData (Moq-tested)
public sealed class CreateExpenseCommandHandler
{
    // ctor-inject: IExpenseData, CreateExpenseCommandValidator, IGroupMembershipGuard,
    //              slice calculators, ILogger<T>   (NO AppDbContext, NO AuditService)
    public async Task<Guid> HandleAsync(
        Guid activityId, CreateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating expense on activity {ActivityId} by user {UserId}", activityId, callerId);
        await _validator.ValidateAndThrowAsync(cmd, ct);   // first statement

        var activity = await _data.GetActivityAsync(activityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");
        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);
        // ... status / currency / category guards via _data reads ...

        var snapshots = await _data.GetActivityParticipantsAsync(activityId, ct);
        // build Expense + ExpenseParticipants in memory; snapshot weights; SplitCalculator.CalculateShares(...)

        var audit = new AuditEntry(callerId, "Expense", expense.Id, "Created", /* metadata */);
        await _data.PersistNewExpenseAsync(expense, participants, audit, ct);   // Add + SaveChanges + audit, atomically
        _logger.LogInformation("Expense {ExpenseId} created ... by user {UserId}", expense.Id, callerId);
        return expense.Id;
    }
}

// DATA — the seam impl; the ONLY write-side EF in the slice (Testcontainers-tested)
internal sealed class ExpenseData : IExpenseData
{
    // ctor-inject: AppDbContext, AuditService, ILogger<T>
    public async Task PersistNewExpenseAsync(Expense e, IReadOnlyList<ExpenseParticipant> ps, AuditEntry audit, CancellationToken ct)
    {
        _db.Expenses.Add(e);
        _db.ExpenseParticipants.AddRange(ps);
        _audit.Queue(audit);              // flushed in the same SaveChanges → atomic
        await _db.SaveChangesAsync(ct);
    }
}
```

Endpoint mapping per use case (`{UseCase}Endpoint`, internal static, single `Map(RouteGroupBuilder)`):

```csharp
internal static class CreateExpenseEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/", async (Guid groupId, Guid activityId, CreateExpenseCommand cmd,
                                CreateExpenseCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(activityId, cmd, ctx.User.GetUserId(), ct);
            return Results.Created(
                $"/groups/{groupId}/activities/{activityId}/expenses/{id}",
                new CreatedResponse(id));
        });
}
```

Rules: handlers sealed, registered scoped, namespace `FamilySplit.Features.{Slice}.{UseCase}` (the gateway lives in `FamilySplit.Features.{Slice}.Data`). Endpoint classes never touch `AppDbContext`. **Command handlers never touch `AppDbContext`/`DbSet<>`/`SaveChangesAsync` — only `{Slice}Data` does** (ADR-001). Happy path at the bottom, early returns for guards (existing repo convention). Logging: `LogDebug` entry in the handler, `LogInformation` on successful mutation (commands only).

### Client update recipe (per slice with mutations)

1. **Refit interface** (`Client/Services/I{X}Client.cs`): creates return `Task<CreatedResponse>`, other mutations return `Task` — remove the old DTO return types. Add a client-side `CreatedResponse` record once (client DTOs stay duplicated by design).
2. **Fluxor effects**: after a successful mutation, dispatch the existing Load action(s) instead of consuming a returned DTO (e.g. `CreateExpenseAction` effect → `await client.Create(...)` → dispatch `LoadExpensesAction(...)`; Join group → navigate using the returned id, then `LoadGroupsAction`). Reducers that patched state from command responses now rely on the re-query.
3. **Client unit tests**: update the affected `Effects*Tests` (mock setup + dispatched-action assertions) and Refit-contract tests.
4. UI pages/components need no edits unless they read a command response directly (audit while migrating the slice).

### Test conventions

Tests follow the ADR-001 layer split — **business logic is mocked, data access uses Testcontainers**:

| Code under test | Project | Strategy |
|---|---|---|
| **Command handlers** (business logic) | `FamilySplit.UnitTests` | **Moq** over `I{Slice}Data` + `IGroupMembershipGuard` — **no DbContext, no InMemory provider** |
| **Validators, pure calculators** (`SplitCalculator`, …) | `FamilySplit.UnitTests` | plain unit tests (no mocks — they take plain data) |
| **`{Slice}Data` gateway** (write-side data access) | `FamilySplit.IntegrationTests` | **Testcontainers** — resolve the gateway against the real Postgres, seed, assert persisted rows |
| **Query handlers** (read-side data access) | `FamilySplit.IntegrationTests` | **Testcontainers** — directly, and/or via the existing query-endpoint tests (the read-side wire-format lock) |

- Command-handler unit tests: `tests/FamilySplit.UnitTests/Features/{Slice}/{UseCase}/{UseCase}CommandHandlerTests.cs` (+ `{UseCase}CommandValidatorTests.cs` beside it). Arrange the `Mock<I{Slice}Data>` to return the read records; **assert** (a) the returned id, (b) the calculator output baked into the entity passed to the mock, and (c) `Verify(...)` that the correct `Persist…Async` was called (or **not** called on a guard/validation failure).
- **The InMemory-`DbContext` `{Slice}TestBase` is removed** — command tests own no DbContext. A Testcontainers `{Slice}DataTestBase` (seed helpers + real `AppDbContext` from the shared connection) backs the gateway/query tests instead.
- Data-access (gateway + query) tests: `tests/FamilySplit.IntegrationTests/Features/{Slice}/...`. Mirror the EF behaviour the old service relied on — `AsNoTracking` projections, filtered indexes, soft-delete filters, the EF-Core-10 cycle-detection workaround.
- Old service-test files are sectioned with `// ── MethodAsync ──` region comments — still the cut points. Read-side assertions move to Testcontainers; command-side assertions move from "inspect returned DTO / InMemory state" to "Moq `Verify` + returned id".
- **Parity gate:** combined new test count (unit command + integration data + validator) >= old service-test file count (minus deliberately dropped per-class constructor tests). Delete the old file in the same commit.
- Endpoint tests (`Endpoints/*Tests.cs`) keep their literal route-pattern asserts; their `CreateApp()` swaps service registration for `new XModule().RegisterServices(...)` / `MapEndpoints(...)` — they become the per-module DI tests (and now also assert `I{Slice}Data` is registered).
- IntegrationTests: query-endpoint tests untouched; mutation-endpoint tests updated in the same slice phase to assert `201 + Location + {id}` / `204` and to follow up with a GET where the old test asserted response-body content.

### Architecture rules (NetArchTest — `tests/FamilySplit.UnitTests/Architecture/`)

**Enforced everywhere, no opt-out.** These rules run against **every** assembly in `FeatureAssemblies.All` — a slice is not "done" until it passes them, and there is no per-slice exemption. Rules 1–7 already existed (Phase 2). ADR-001's **revised Rule 5** and **new Rules 8/10/11** are now **live** (implemented in the Expenses retrofit): Rule 11 ships as `DoesNotReferenceDbContextRule` (Mono.Cecil) and `DoesNotCallSaveChangesRule` is now applied to command handlers too. (Rule 9 — the `I{Slice}Data` / `{Slice}Data` declaration check — remains documented but unimplemented; it is currently upheld by Rules 5+8+10 plus review.)

| Rule | Statement |
|---|---|
| 1–4, 6, 7 | (unchanged) reference allow-list · one `IFeatureModule` per slice · handlers sealed + in use-case namespace · validator co-located · registry completeness · query handlers don't depend on mutation services |
| **5 (revised)** | `AppDbContext` may be taken only by `*QueryHandler`, `*Data` (the gateway), `*Seeder`, and the Common guard impl. **`*CommandHandler` must NOT take `AppDbContext`.** |
| **8 (new)** | Each `*CommandHandler` constructor depends on an `I{Slice}Data` interface and references **no** `AppDbContext`/`DbSet<>`/concrete `*Data` type — enforces mockability. |
| **9 (new)** | A slice with commands declares exactly one `I{Slice}Data` (public) + one `{Slice}Data` (`internal sealed`, in the `…​.Data` namespace) registered scoped. Query-only slices declare neither. |
| **10 (new)** | `SaveChangesAsync` is called only by `*Data` gateways (+ Common audit/guard impl). Extend `DoesNotCallSaveChangesRule` to also fail `*CommandHandler` types (today it covers `*QueryHandler`). |
| **11 (new, test-side — required)** | Custom rule scanning `FamilySplit.UnitTests.Features.*` command-handler tests for an `AppDbContext`/`DbContextOptions`/`UseInMemoryDatabase` reference → fail (business-logic tests must mock `I{Slice}Data`, never touch a provider). |

> The separation is **not** a per-slice judgement call: any new slice that holds business logic must split it from its data access, or the architecture suite goes red. Query-only slices already satisfy it (their query handler *is* the data-access layer — there is no business logic to separate); they are not an exception to the rule.

### Per-slice phase checklist (the same steps every time)

1. Create `src/Features/FamilySplit.Features.{X}/` project (template above).
2. Add to `FamilySplit.slnx` under `/src/Features/` solution folder.
3. Add COPY line to `src/FamilySplit.Api/Dockerfile` restore block.
4. Api csproj: add ProjectReference; Program.cs: add `new {X}Module()` to the array; remove the old `Map{X}Endpoints()` call.
5. Classify each use case **Command or Query** (inventory below); create use-case folders; dissolve the service into handlers (copy bodies; queries get `AsNoTracking`; commands drop the final detail-read and return ids/nothing; swap `NotFound()`/`Throw422()` → `ValidationErrors`; swap private guards → `IGroupMembershipGuard` where applicable); move slice-local calculators/guards into `Shared/`; DTOs into their query folders (or `Shared/` if multi-use-case).
6. **If the slice has commands, build the `Data/` seam (ADR-001):** define `I{Slice}Data` (reads as plain records + per-use-case persist methods that own `Add`/`Update`/`Remove` + `SaveChangesAsync` + audit flush) and `{Slice}Data` (`internal sealed`, the **only** write-side `AppDbContext`/`SaveChanges`). Move every EF read/write out of the command handlers into it; register `AddScoped<I{Slice}Data, {Slice}Data>()` in the module. Query-only slices skip this step.
7. Delete the old `Api/Endpoints/{X}Endpoints.cs`, the `Application/{X}/` folder, and the slice's lines in `Application/DependencyInjection.cs`.
8. UnitTests: add ProjectReference to the new feature project; **command-handler tests mock `I{Slice}Data` (Moq, no DbContext)**; validator/calculator tests stay plain; delete old files same commit.
9. IntegrationTests: add **Testcontainers tests for `{Slice}Data` + query handlers**; update this slice's **mutation** endpoint tests to the new response shapes (+ follow-up GETs); leave query-endpoint tests untouched.
10. Client: apply the client update recipe (Refit signatures, effects re-query, client unit tests).
11. Append the slice assembly to `tests/FamilySplit.UnitTests/Architecture/FeatureAssemblies.cs`; ensure Rules 5 + 8–11 pass for it.
12. Verify: `dotnet build FamilySplit.slnx -c Release` && UnitTests && IntegrationTests && Client.UnitTests green.
13. Commit (one commit per slice).

---

## Phases

### ✅ Phase 0 — Prep (DONE — commit `7034697`)

Deleted 30 empty stray test files (`UnitTests/Store/**`, `UnitTests/Services/**`); removed Client.UnitTests' dead `FamilySplit.Application` reference. Baseline recorded: 688 unit / 431 client / 96 integration.

### ✅ Phase 1 — Extract `FamilySplit.Common` (DONE — commit `514509a`)

All 10 Common source files scaffolded, CreatedResponse added, slnx + Dockerfile + csprojs updated,
originals deleted, namespaces updated across ~20 files, inline ClaimsPrincipalExtensions removed from
Program.cs, GroupMembershipGuard injected into 4 services replacing duplicated private guards,
test files moved to Common/, AddFamilySplitCommon test added.
689 unit / 431 client tests green; build: 0 warnings, 0 errors.

### ✅ Phase 2 — Architecture-test scaffolding (DONE — commit `04b8602`)

NetArchTest.Rules 1.3.2 added; `tests/FamilySplit.UnitTests/Architecture/` scaffolded with
`FeatureAssemblies.cs` (registry, empty), `DoesNotCallSaveChangesRule.cs` (Mono.Cecil IL rule),
and `ArchitectureTests.cs` (11 tests covering Rules 1–7). All rules vacuously green.
700 unit / 431 client tests green; build: 0 warnings, 0 errors.

### ✅ Phase 3 — Pilot slice: **Expenses** (DONE — proves all conventions incl. the CQRS response change end-to-end)

| Use case | Kind | Response |
|---|---|---|
| List | Query | unchanged |
| GetDetail | Query | unchanged (absorbs `BuildDetailDtoAsync`) |
| Create | Command | **201 + `{id}`** (was: detail DTO) |
| Update | Command | **204** (was: detail DTO) |
| Delete | Command | 204 (unchanged) |

`Shared/`: `ExpenseParticipantDto` (List+GetDetail), `SplitCalculator`, `ExpenseReshuffleRequired`, `ExpenseGuards` (`RequireSameFamilyAsPayerOrGlobalAdminAsync` + currency/category checks — used by Create+Update+Delete).
Also: delete `Application/Core/SplitCalculator.cs` + `Expenses/`; move `Core/SplitCalculatorTests.cs` + `BusinessGuardTests`' reshuffle section → `Features/Expenses/`; split `ExpenseServiceTests.cs` (883 lines — region comments are the cut points) with `ExpenseTestBase`.
Client: `IExpenseClient` create/update signatures; Expense effects re-query (`LoadExpensesAction` / `LoadExpenseDetailAction`); reducers stop patching from command responses; effects tests.
**Extra gates for the pilot:** run the E2E suite locally (expense flow exercises the re-query path through the real UI); verify Scalar lists the expense endpoints in dev.

**Completion notes:** `FamilySplit.Features.Expenses` created (5 use-case folders + `Shared/`), wired into slnx / Dockerfile / Api csproj / `Program.cs` (`new ExpensesModule()`); `ExpenseGuards` implemented as static helpers taking `AppDbContext` (so Rule 5's ctor-injection check stays satisfied). `ExpenseAmountRules` moved to `Shared/`. Old `Application/Expenses/`, `Application/Core/SplitCalculator.cs`, `Api/Endpoints/ExpenseEndpoints.cs`, and the Application-DI line deleted. `FeatureAssemblies.All` now registers the slice — all 11 architecture rules apply (two latent Phase-2 `because:`-string NREs fixed: null-guard `result.FailingTypes`). Tests: ExpenseServiceTests split per-handler with `ExpenseTestBase`; validator tests split Create/Update; SplitCalculator + reshuffle tests moved; endpoint test rewritten as a per-module DI/route test; integration Update tests flipped to 204 + follow-up GET; client effects/reducers re-query. **Verification:** Release build 0 warnings/0 errors; 699 unit / 431 client / 96 integration green; E2E 17/18 (the 3 expense flows pass — re-query path proven through the UI). The 1 E2E failure (`SettlementFlowTests.FullSettlementLifecycle`) is **pre-existing and unrelated** — it seeds a Closed activity directly in the DB, but settlement generation only fires through the close flow, so the mark-sent button never appears. Scalar listing covered-by-proxy (integration tests exercise all 5 routes through the real host; unit test asserts the module maps them with `WithTags("Expenses")`).

> ✅ **Retrofit done (ADR-001).** `IExpenseData` + `ExpenseData` (`internal sealed`) added under `Data/` — the only write-side EF in the slice; it absorbs the activity/participant/currency/category reads, the `Add`/`Update`/`Delete` + `SaveChangesAsync`, and the atomic `AuditService.Queue` flush. The three command handlers are now **EF-free** (depend on `IExpenseData` + `IGroupMembershipGuard` + pure calculators); `ExpenseGuards` is pure. **Common** gained `IGroupMembershipGuard` (the concrete `GroupMembershipGuard` implements it; both DI-registered, interface forwards to the scoped concrete) and a reusable `AuditEntry` + `AuditService.Queue(AuditEntry)` overload. Tests: command tests rewritten as `Mock<IExpenseData>`/`Mock<IGroupMembershipGuard>` (no DB) via a new `ExpenseCommandTestBase`; the InMemory `ExpenseTestBase` + the two InMemory query-handler tests were deleted (their data-access coverage is the existing Testcontainers endpoint tests). Arch rules went live: Rule 5 tightened, Rules 8/10 added, Rule 11 added via a new `DoesNotReferenceDbContextRule` (Mono.Cecil). **Verified:** 684 unit/arch + full integration suite green; Release build 0/0.

### ✅ Phase 4 — Dashboard (DONE)
`GetStats` (Query — single handler from `DashboardService`). No mutations → no client/integration-test changes. Smallest slice — fast confirmation of the pattern.

**Completion notes:** `FamilySplit.Features.Dashboard` created (single `GetStats/` use-case folder: `GetStatsQueryHandler` + `DashboardGroupStatDto` + `GetStatsEndpoint`, plus `DashboardModule`). csproj omits FluentValidation (no command/body → no validators), so `RegisterServices` registers only the one handler and does **not** call `AddValidatorsFromAssembly`. Handler body copied verbatim from `DashboardService.GetStatsAsync`, with `ILogger<GetStatsQueryHandler>` injected (LogDebug entry) and `.AsNoTracking()` added to each entity-rooted query (read-side intent). Route unchanged (`GET /dashboard/stats` → `Results.Ok(stats)`), so the read-side wire format is preserved — client DTO + effects + reducers untouched. Wired into slnx (`/src/Features/`), Dockerfile COPY, Api csproj, and `Program.cs` (`new DashboardModule()` in the module array; `app.MapDashboardEndpoints()` removed). Old `Application/Dashboard/` (service + DTOs), `Api/Endpoints/DashboardEndpoints.cs`, and the Application-DI line deleted. Tests: `DashboardServiceTests` (11) → `Features/Dashboard/GetStats/GetStatsQueryHandlerTests` (11, `GetStatsAsync_*`→`Handle_*`, self-contained — no separate TestBase needed for a single handler); `DashboardEndpointsTests` rewritten as a per-module DI/route test (2→4); `DependencyInjectionTests` DashboardService assertion removed; `FeatureAssemblies.All` now registers the Dashboard assembly so all 11 architecture rules apply. **Verification:** Release build 0 warnings/0 errors; 701 unit (was 699) / 431 client green. No Dashboard integration tests exist (query-only, unchanged route) and the client is untouched, so nothing else to run.

> ✅ **Retrofit done (ADR-001).** Dashboard is **query-only**, so there is **no `I{Slice}Data`** — `GetStatsQueryHandler` *is* data access and keeps `AppDbContext`. The InMemory `GetStatsQueryHandlerTests` (12 scenarios) was ported to **Testcontainers** as `IntegrationTests/Dashboard/DashboardStatsTests` (seeded via EF through the shared test transaction, asserted through `GET /dashboard/stats`) and the InMemory unit test deleted — proving query-only handlers go straight to Testcontainers with no mocking. The port surfaced a real `(activity_id, payer_family_id, receiver_family_id)` unique index that the InMemory provider had masked.

### ✅ Phase 5 — Users (DONE)
`WhoAmI` (Query — inline `db.Users` projection from `UserEndpoints.cs`). No mutations. **Query-only → no `I{Slice}Data`**; the `WhoAmI` query handler uses `AppDbContext` directly and is Testcontainers-tested.

**Completion notes:** `FamilySplit.Features.Users` created (single `WhoAmI/` use-case folder: `WhoAmIQueryHandler` + `WhoAmIDto` + `WhoAmIEndpoint`, plus `UsersModule`). csproj omits FluentValidation (query-only, no validators) — mirrors Dashboard. Handler is a single `AsNoTracking` projection of the caller's own `User` row keyed on `callerId` (no guard needed — it reads only the caller's own row); `Provider` projected via `.ToString()` to preserve the legacy JSON shape exactly; returns `WhoAmIDto?`, endpoint maps null → 404. `/whoami` is a top-level route, so `WhoAmIEndpoint.Map(IEndpointRouteBuilder)` maps it directly with `.WithTags("Users")` (no `MapGroup`). Wired into slnx (`/src/Features/`), Dockerfile COPY, Api csproj, `Program.cs` (`new UsersModule()` added to the module array; `app.MapUserEndpoints()` removed), and `FeatureAssemblies.All` (all 11 architecture rules now apply — Rules 8/10/11 are vacuous for a query-only slice). Old `Api/Endpoints/UserEndpoints.cs` deleted (no Application service existed — WhoAmI was inline). Tests: `UserEndpointsTests` (3, `MapUserEndpoints` extension) → `UsersEndpointsTests` (4, per-module DI/route test mirroring `DashboardEndpointsTests`); added Testcontainers `IntegrationTests/Users/WhoAmITests` (2: full-projection-shape assertion + unknown-user → 404). The pre-existing `AuthProofTests` (`ProofTests.cs`) `/whoami` happy-path + 401 tests are the read-side wire-format lock — left untouched and still green through the new slice. Client untouched (`IWhoAmIApi` + its contract test unchanged — route and wire format preserved). **Verification:** Release build 0 warnings/0 errors; 685 unit green (incl. architecture suite over the Users assembly); 4 relevant integration tests green (2 WhoAmI Testcontainers + 2 AuthProof lock).

### ✅ Phase 6 — Groups (DONE)
Queries: `List`, `GetDetail`. Commands: `Create` (**201+id**), `Update` (**204**), `Join` (**200+`{id}` — documented exception**), `Leave` (204), `RegenerateInviteCode` (**resolved: 204 + re-query** — `GroupDetailDto` exposes the code to admins).
`Shared/`: group-admin guard (interface), invite-code helper, member-mapping DTOs (uses Common `WeightCalculator`). **`Data/`:** `IGroupData` + `GroupData` (the 5 commands' reads/writes incl. invite-code uniqueness). Delete `GroupMembersEndpoints.cs` stub. Client: `IGroupClient` + Group effects (Join navigates via returned id, then re-queries).

**Completion notes:** `FamilySplit.Features.Groups` created — 2 query folders (`List`, `GetDetail`), 5 command folders (`Create`, `Update`, `Join`, `Leave`, `RegenerateInviteCode`), `Data/` (`IGroupData` + `internal sealed GroupData`), `Shared/InviteCodeGenerator` (pure RNG; uniqueness loop stays in `GroupData`), `GroupsModule`. **Auth seam:** command handlers depend on `IGroupMembershipGuard` (for `GetCallerFamilyIdAsync`, mockable) + `IGroupData` for the group-specific reads `IsActiveFamilyAdminAsync` / `GetFamilyRoleInGroupAsync` and throw `ForbiddenException` themselves — no separate guard interface needed (single seam, ADR-001-clean). Query handlers inject the concrete `GroupMembershipGuard` + `AppDbContext` (mirrors Dashboard). **RegenerateInviteCode resolved to 204** (the verify-point in the plan): `GroupDetailDto.InviteCode` already exposes the code to admins, so the client re-queries detail. **Join** is the documented 200 + `{id}` exception (no `Location`). Group mutations are non-financial → `GroupData` has **no `AuditService`** (CLAUDE.md). Wired into slnx / Dockerfile / Api csproj / `Program.cs` (`new GroupsModule()`; `MapGroupEndpoints` + `MapGroupMemberEndpoints` removed) / `FeatureAssemblies.All`. Deleted `Application/Groups/` (service + validators + DTOs), `Api/Endpoints/GroupsEndpoints.cs`, the `GroupMembersEndpoints.cs` stub, the `GroupService` DI line, and the `DependencyInjectionTests` GroupService assertion. **Tests:** command-handler unit tests (Moq over `IGroupData` + `IGroupMembershipGuard`, no DB) for all 5 commands + `GroupCommandTestBase`; Create/Update/Join validator tests; `GroupsEndpointsTests` rewritten as a per-module DI/route test; `IntegrationTests/Groups/GroupEndpointsTests` rewritten to the new 201/204/200 shapes (+ follow-up GETs) preserving the 403/422 boundaries. **Client:** `IGroupClient` create/join → `Task<CreatedResponse>`, update/regenerate → `Task` (dropped `RegenerateInviteCodeResponse`); Group effects re-query after every mutation (create/join navigate to `/groups/{id}` then `LoadGroupsAction`; update/regenerate dispatch `LoadGroupDetailAction` + `LoadGroupsAction`); success actions now carry the `Guid` id, not a DTO; reducers/`GroupList.razor` simplified (removed brittle post-render navigation polling). **Verification:** Release build 0 warnings/0 errors; **689 unit** (incl. architecture suite over the Groups assembly), **429 client**, **114 integration** (full suite — the 17 new Groups tests + no regression in Activities/Settlements/Expenses/Dashboard/Users/Auth). Backend tests + client migration were executed as two parallel sub-agents over disjoint file trees, then unified-verified.

### ✅ Phase 7 — Activities (DONE)
Queries: `List`, `GetDetail`. Commands: `Create` (**201+id**), `CreateSubActivity` (**201+id**), `Update` (204), `Close` (204), `AddParticipant` (204), `RemoveParticipant` (204).
`Shared/`: `ParticipantSeeder`, `ActivityCloseGuard`, participant DTOs (detail builder collapses into GetDetail query). **`Data/`:** `IActivityData` + `ActivityData` (the 6 commands' reads/writes; `ParticipantSeeder` stays pure and runs in the handler on records returned by `IActivityData`). Move `Core/ParticipantSeederTests.cs` + close-guard section of `BusinessGuardTests` along (pure → stay in UnitTests). Client: `IActivityClient` + Activity effects.

**Completion notes:** `FamilySplit.Features.Activities` created — 2 query folders (`List`, `GetDetail`), 6 command folders (`Create`, `CreateSubActivity`, `Update`, `Close`, `AddParticipant`, `RemoveParticipant`), `Data/` (`IActivityData` + `internal sealed ActivityData`), `Shared/` (`ParticipantSeeder` now **pure** — maps member-ids → `ActivityParticipant` rows; `ActivityCloseGuard`; `ActivitySummaryDto`; `ActivityParticipantDto`), `ActivitiesModule`. Built ADR-001-clean from the start (no retrofit). **CreateSubActivity reuses `CreateActivityCommand` + `CreateActivityCommandValidator`** (same request shape) — its folder holds only a handler + endpoint. **Auth seam:** command handlers depend on `IGroupMembershipGuard.RequireGroupMemberAsync` (mockable) + `IActivityData` for the activity reads; query handlers inject the concrete `GroupMembershipGuard` + `AppDbContext` (mirrors Dashboard/Groups). Activity mutations are non-financial → `ActivityData` has **no `AuditService`**. The DB reads that decide *which* members are seeded (active group members; parent's participants) moved from the old DB-reading `ParticipantSeeder` into `IActivityData` (`GetActiveGroupMemberIdsAsync` / `GetActivityParticipantMemberIdsAsync`). `Close` keeps the open-sub absorb logic in the gateway (`CloseActivityAsync` returns the absorbed count for the handler's log line). Wired into slnx (`/src/Features/`) / Dockerfile COPY / Api csproj / `Program.cs` (`new ActivitiesModule()`; `MapActivityEndpoints` removed) / `FeatureAssemblies.All`. Deleted `Application/Activities/` (service + validators + DTOs + `ActivityCloseGuard`), `Application/Core/ParticipantSeeder.cs`, `Api/Endpoints/ActivityEndpoints.cs`, and the two `DependencyInjection` lines (`ParticipantSeeder`, `ActivityService`). **Tests:** command-handler unit tests (Moq over `IActivityData` + `IGroupMembershipGuard`, no DB) for all 6 commands + `ActivityCommandTestBase`; Create/Update/AddParticipant validator tests; pure `ParticipantSeederTests` + `ActivityCloseGuardTests` moved into `Features/Activities/Shared`; `ActivitiesEndpointsTests` per-module DI/route test (8 routes); old `ActivityServiceTests` / `ActivityValidatorTests` / unit `ActivityEndpointsTests` / `Core/ParticipantSeederTests` deleted and the `ActivityCloseGuard` section removed from `BusinessGuardTests`; `DependencyInjectionTests` Activity assertions removed. Integration `Activities/ActivityEndpointsTests` rewritten to the new 201/204 shapes (+ follow-up GETs) with a file-local `ActivityTestBase`, preserving the 403/422 boundaries and adding active-only / sub-copies-parent participant-seeding coverage through the detail endpoint. **Client:** `IActivityClient` create/sub → `Task<CreatedResponse>`, update/close/add/remove → `Task`; Activity effects re-query after every mutation (create → `LoadActivitiesAction`; sub/update/add/remove → `LoadActivityDetailAction` (+ `LoadBalancesAction` for participant changes); close → re-query detail + list + generate settlements + balances); success actions now carry the `Guid` id; reducers' success branches just stop loading (the OnCreateSubSuccess append logic is gone — the parent re-query repopulates the sub list); `QuickExpenseDialog.CreateActivityAsync` builds its local summary from inputs + the returned id. **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **690 unit** (incl. architecture suite over the Activities assembly), **427 client** green. Integration tests compile clean but were **not executed locally** (Docker/Testcontainers unavailable in the dev environment) — they run in CI.

### ✅ Phase 8 — Settlements (DONE)
Queries: `GetBalances`, `List`, `GetDetail`, `ListForGroup`, `ListMyPending`. Commands: `Generate` (**204** — idempotent, client re-queries the list; was: summary list), `ConfirmSent` (**204**, was detail DTO), `ConfirmReceived` (**204**).
`Shared/`: `BalanceCalculator`, `SettlementOptimiser`, `SettlementStateMachine`, summary DTOs (all pure → stay in UnitTests). **`Data/`:** `ISettlementData` + `SettlementData` absorbs the old `SettlementQueryHelpers` (LoadExpenseData/GetActivityCurrency/GetActivityAndSubIds) plus the Generate/ConfirmSent/ConfirmReceived persistence. Note: those helpers were shared by queries **and** the Generate command — under ADR-001 the **query handlers** call `AppDbContext` directly and the **Generate command** calls `ISettlementData`; the shared SQL is duplicated or both delegate to `SettlementData` (queries may use the gateway's read methods since it's data access too). `INotificationService` (Common) is invoked from `SettlementData`'s persist so the command stays EF-free; impl still host-registered until Phase 11. Largest test split (991 lines, 8 handlers): commands → Moq, calculators/state-machine → plain, gateway/queries → Testcontainers. Client: `ISettlementClient` + Settlement effects (mark-sent/mark-received re-query settlements + balances).

**Completion notes:** `FamilySplit.Features.Settlements` created — 5 query folders (`GetBalances`, `List`, `GetDetail`, `ListForGroup`, `ListMyPending`), 3 command folders (`Generate`, `ConfirmSent`, `ConfirmReceived`), `Data/` (`ISettlementData` + `internal sealed SettlementData`), `Shared/` (`BalanceCalculator`, `SettlementOptimiser`, `SettlementStateMachine` — all pure — plus the five response DTOs), `SettlementsModule`. Built ADR-001-clean from the start (no retrofit). **No validators** (the three commands take only route ids), so the csproj omits FluentValidation and the module skips `AddValidatorsFromAssembly` (mirrors Dashboard/Users). **Auth seam:** command handlers depend on `IGroupMembershipGuard` (`RequireGroupMemberAsync` + `GetCallerFamilyIdAsync`, both mockable) + `ISettlementData`; query handlers inject the concrete `GroupMembershipGuard` + `AppDbContext` (mirrors Activities/Groups), except `ListMyPending` which resolves the caller-family inline (the only query that throws `ForbiddenException` directly, matching the old service). **Data seam decisions:** the Generate idempotency/race-catch (`DbUpdateException` → swallow if the winner's rows exist) lives in `SettlementData.GenerateSettlementsAsync`; the *decisions* (status guards, zero-balance → `MarkActivitySettledAsync`, balance calc + optimise, "all others completed → mark activity Settled") stay in the command handlers. Settlements are **financial**, so `SettlementData` flushes `AuditService.Queue` atomically with each mutation (Generated/ConfirmSent/ConfirmReceived) — and, per the plan, `INotificationService.NotifyFamilyAsync` is invoked from the gateway *after* the save (same request scope) so the command stays EF-free. `GetBalances` duplicates the small currency/balance-input SQL with the gateway (ADR-001 allows it — both are data access). **Strict-CQRS wire change:** Generate/ConfirmSent/ConfirmReceived now return **204** (were: summary list / detail DTO); the client re-queries. Read endpoints (balances, list, detail, group settlements, dashboard pending) keep their JSON shapes. Wired into slnx / Dockerfile COPY / Api csproj / `Program.cs` (`new SettlementsModule()`; `MapSettlementEndpoints` removed) / `FeatureAssemblies.All` / UnitTests csproj. Deleted `Application/Settlements/` (service + state machine + DTOs), `Application/Core/BalanceCalculator.cs` + `SettlementOptimiser.cs` (the `Application/Core` folder is now empty), `Api/Endpoints/SettlementEndpoints.cs`, and the `SettlementService` DI line + `DependencyInjectionTests` assertion. **Tests:** command-handler unit tests (Moq over `ISettlementData` + `IGroupMembershipGuard`, no DB) for all 3 commands + `SettlementCommandTestBase`; `BalanceCalculatorTests` / `SettlementOptimiserTests` moved to `Features/Settlements/Shared`; the `SettlementStateMachine` section of `BusinessGuardTests` extracted to `Features/Settlements/Shared/SettlementStateMachineTests` (and `BusinessGuardTests` deleted — it was empty); old `SettlementServiceTests` (InMemory, 50+ cases) deleted; `Endpoints/SettlementEndpointsTests` rewritten as a per-module DI/route test (`SettlementsEndpointsTests`, 8 routes, asserts the `ISettlementData` registration). Integration `Settlements/SettlementEndpointsTests` rewritten to the new 204 + follow-up-GET shapes (generate/confirm-sent/confirm-received), preserving the 403/422 boundaries; a shared `GenerateAndGetFirstSettlementIdAsync` helper replaces reading the id from the (now-empty) generate response. **Client:** `ISettlementClient` generate/confirm-sent/confirm-received → `Task`; the three success actions are now payload-free; effects re-query after every mutation (generate → list + activity detail; confirm-sent/received → settlement detail + list + group + dashboard, confirm-received also activity detail); reducers' success branches just clear the loading flag (the brittle list-patching is gone — re-query repopulates); the razor pages were untouched (they only dispatch the input actions). **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **675 unit** (incl. architecture suite over the Settlements assembly — Rules 5/8/10/11 confirm the command handlers hold no `AppDbContext`/`SaveChanges` and the gateway is the only write-side EF), **425 client** green. Integration tests compile clean but were **not executed locally** (Docker/Testcontainers unavailable) — they run in CI.

### ✅ Phase 9 — Admin (DONE — MUST precede Families)
Queries: `ListFamilies`, `GetFamily`. Commands: `CreateFamily` (**201+id**), `AddFamilyMember` (**201+id**), `UpdateFamilyMember` (204), `RemoveFamilyMember` (204), `DeleteGroup` (204), `AddFamilyToGroup` (204), `RemoveFamilyFromGroup` (204).
`Shared/`: `RequireGlobalAdmin` guard (interface) + **own copies** of FamilyDto/FamilyMemberDto + member validators (identical JSON property names on the query side — `IntegrationTests/Admin` query tests lock it). **`Data/`:** `IAdminData` + `AdminData` (family/member/group-link reads + writes; `RequireGlobalAdminAsync` is a DB read → expose mockably so command tests stub the admin check). Client: `IAdminClient` + Admin effects.

**Completion notes:** `FamilySplit.Features.Admin` created — 2 query folders (`ListFamilies`, `GetFamily`), 7 command folders (`CreateFamily`, `AddFamilyMember`, `UpdateFamilyMember`, `RemoveFamilyMember`, `DeleteGroup`, `AddFamilyToGroup`, `RemoveFamilyFromGroup`), `Data/` (`IAdminData` + `internal sealed AdminData`), `Shared/` (own-copy `FamilyDto`/`FamilyMemberDto`, `FamilyMemberMapper` over Common `WeightCalculator`, `AdminGate`), `AdminModule`. Built ADR-001-clean from the start. **Global-admin gate, two routes:** the *command* side uses the mockable `IAdminData.IsGlobalAdminAsync` seam (so command tests stub the admin check — no separate guard interface needed); the *query* side (which already holds `AppDbContext`) calls the static `AdminGate.RequireGlobalAdminAsync(db, …)` helper. Neither trips the arch rules — Rule 5 only checks ctor-injected `AppDbContext` (a static helper param is unscoped), and Rule 11 only scans `*CommandHandlerTests`. Admin mutations are **non-financial** → `AdminData` has no `AuditService` (CLAUDE.md). The `DeleteGroup` transactional delete (sub-activities first, then the group cascade, via the retrying execution strategy) lives in the gateway and returns a found/not-found bool the handler maps to 422. **Strict-CQRS wire change:** `CreateFamily`/`AddFamilyMember` now return **201 + {id}** (were full FamilyDto / FamilyMemberDto); `UpdateFamilyMember` now **204** (was member DTO). The read endpoints (List/GetFamily) keep their JSON shapes. Wired into slnx / Dockerfile COPY / Api csproj / `Program.cs` (`new AdminModule()`; `MapAdminEndpoints` removed) / `FeatureAssemblies.All` / UnitTests csproj. Deleted `Application/Admin/` (service + validator + DTOs), `Api/Endpoints/AdminEndpoints.cs`, the `AdminService` DI line + its `DependencyInjectionTests` assertion. **Note:** `Application/Families` stays (Phase 10) — Admin no longer references it (own DTO/validator/mapper copies). **Tests:** command-handler unit tests (Moq over `IAdminData`, no DB) for all 7 commands + `AdminCommandTestBase`; Create/AddMember/UpdateMember validator tests; `Endpoints/AdminEndpointsTests` rewritten as the per-module DI/route test (9 routes); old `Admin/AdminServiceTests` + `Admin/AdminValidatorTests` deleted. Integration `Admin/AdminEndpointsTests` mutation tests updated to the 201+{id} / 204 + follow-up-GET shapes, preserving the 403/422 boundaries and the query wire-format lock. **Client:** `IAdminClient` create/add-member → `Task<CreatedResponse>`, update-member → `Task`; Create effect re-queries `LoadAdminFamilies`, UpdateMember effect re-queries `LoadAdminFamily`; success actions carry no DTO (create parameterless, update carries the family id); reducers' success branches just clear the loading flag (list-append / member-patch removed). The Admin razor pages were untouched (they only dispatch the input actions). **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **687 unit** (incl. architecture suite over the Admin assembly), **424 client** green. Integration tests compile clean but were **not executed locally** (Docker/Testcontainers unavailable) — they run in CI.

### ✅ Phase 10 — Families (DONE)
Queries: `GetMyFamily`, **`GetMyProfile`** (absorbs `GET /users/me/profile`; maps its own `/users/me` group). Commands: `UpdateFamilyName` (204), `AddMember` (**201+id**), `UpdateMember` (204), `RemoveMember` (204).
`Shared/`: family-admin guard (interface), member mapper (uses Common `WeightCalculator`), shared member DTOs. **`Data/`:** `IFamilyData` + `FamilyData` (own-family reads + member writes; reused by the absorbed `GetMyProfile`). Delete `FamilyEndpoints.cs`, `FamilyMembersEndpoints.cs`, `Application/Families/`, and the tombstone `Application/FamilyMembers/`. Client: `IFamilyClient` + Family effects.

**Completion notes:** `FamilySplit.Features.Families` created — 2 query folders (`GetMyFamily`, `GetMyProfile`), 4 command folders (`UpdateFamilyName`, `AddMember`, `UpdateMember`, `RemoveMember`), `Data/` (`IFamilyData` + `internal sealed FamilyData`), `Shared/` (own-copy `FamilyDto`/`FamilyMemberDto`, `FamilyMemberMapper` over Common `WeightCalculator`), `FamiliesModule`. Built ADR-001-clean from the start. **No separate guard interface:** unlike the plan's placeholder ("family-admin guard (interface)"), the admin-or-self resolution folds into `IFamilyData.GetCallerMemberAsync` (returns the caller's own `FamilyCallerMember { Id, FamilyId, IsAdmin }`) — mirroring how Admin folded its global-admin check into `IAdminData.IsGlobalAdminAsync` rather than a standalone guard type; a bespoke guard class would have failed architecture Rule 5 (its `AppDbContext` ctor-injection isn't on the allow-list) unless placed in Common, and this is Families-local, not shared. **Query-side caller resolution is deliberately *not* routed through Common's `IGroupMembershipGuard`:** that guard *throws* `ForbiddenException` when the caller has no active FamilyMember, but the pre-existing wire format for `GetMyFamily`/`GetMyProfile` is a plain **404** in that case (the read-side lock) — so both query handlers resolve the caller inline via `AppDbContext` and return null, exactly matching the legacy `FamilyService` behaviour. `UpdateMember`'s self-elevation guard (`caller.IsAdmin ? cmd.IsAdmin : target.IsAdmin`) required `IFamilyData.GetActiveMemberInFamilyAsync` to return the target's *current* `IsAdmin` flag alongside its email, so a non-admin editing their own profile can't accidentally reset (or self-elevate) their own admin flag. Family mutations are non-financial → `FamilyData` has no `AuditService` (CLAUDE.md). Wired into slnx / Dockerfile COPY / Api csproj / `Program.cs` (`new FamiliesModule()` added to the module array; `MapFamilyEndpoints`/`MapFamilyMemberEndpoints` removed) / `FeatureAssemblies.All` / UnitTests csproj (IntegrationTests picks it up transitively via `FamilySplit.Api`). Deleted `Application/Families/` (service + validator + DTOs), the tombstone `Application/FamilyMembers/`, `Api/Endpoints/FamilyEndpoints.cs`, `Api/Endpoints/FamilyMembersEndpoints.cs`, and the `FamilyService` DI line + its `DependencyInjectionTests` assertion. **Strict-CQRS wire change:** `AddMember` now returns **201 + {id}** (was full `FamilyMemberDto`); `UpdateFamilyName`/`UpdateMember` now **204** (were `FamilyDto`/`FamilyMemberDto`). `RemoveMember` (204) and the read endpoints (`GetMyFamily`, `GetMyProfile`) keep their existing shapes. **Tests:** command-handler unit tests (Moq over `IFamilyData`, no DB) for all 4 commands + `FamiliesCommandTestBase`; UpdateFamilyName/AddMember/UpdateMember validator tests (ported 1:1 from the old `FamilyValidatorTests`); pure `FamilyMemberMapperTests` (ported from the old `ToDto` tests); `Endpoints/FamiliesEndpointsTests` per-module DI/route test (6 routes, 5 under `/families/mine` + `GET /users/me/profile` under `/users/me`); old `Families/FamilyServiceTests` (37 cases) + `Families/FamilyValidatorTests` (40 cases) + unit `Endpoints/FamilyEndpointsTests` + `Endpoints/FamilyMembersEndpointsTests` deleted; `DependencyInjectionTests` FamilyService assertion removed. Integration `Families/FamilyEndpointsTests` mutation tests (`UpdateFamilyName`, `AddMember`, `UpdateMember`) rewritten to the new 204/201 + follow-up-GET shapes, preserving the 403/422 boundaries; `RemoveMember` tests were already 204/422 (no change); added a `GetMyProfileTests` class (happy path + 401) since the absorbed endpoint had no prior direct integration coverage; `GetMyFamily` tests untouched (read-side lock). **Client:** `IFamilyClient` `AddMemberAsync` → `Task<CreatedResponse>`, `UpdateFamilyNameAsync`/`UpdateMemberAsync` → `Task` (dropped the `FamilyDto`/`FamilyMemberDto` returns); `IFamilyMemberClient`/`GetProfileAsync` untouched (query-side, same route and shape); Family effects re-query (`LoadMyFamilyAction`) after every mutation, including `RemoveMember` (previously patched `MyFamily.Members` locally — now consistent with the other migrated slices); success actions are now payload-free; reducers' success branches just clear the loading flag (the member-patch/filter logic is gone — re-query repopulates); `ManageFamily.razor` untouched (it only dispatches the input actions and reads `State.Value.MyFamily`). **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **687 unit** (incl. architecture suite over the Families assembly — Rules 5/8/10/11 confirm the command handlers hold no `AppDbContext`/`SaveChanges` and the gateway is the only write-side EF), **422 client** green. Integration tests compile clean but were **not executed locally** (Docker/Testcontainers unavailable in the dev environment) — they run in CI.

### ✅ Phase 11 — Notifications (DONE)
Query: `GetVapidPublicKey` (AllowAnonymous). Commands: `Subscribe` (204), `Unsubscribe` (204).
Plus `PushNotificationService`, `NotificationHub`, `SignalRNotificationService` (keep the `IServiceScopeFactory` background-push pattern exactly). Module: `AddSignalR()` + `AddScoped<INotificationService, SignalRNotificationService>()` + maps `/push` group and `MapHub<NotificationHub>(HubPaths.Notifications)`. Host drops `Hubs/` folder, `AddSignalR`, the INotificationService registration, and `MapHub`. `Lib.Net.Http.WebPush` package moves from Application csproj to this slice. **`Data/`:** `IPushSubscriptionData` + impl for the Subscribe/Unsubscribe persistence (`GetVapidPublicKey` reads config, not the DB → no data dependency). Client: `IPushClient` signatures if response shapes change.

**Completion notes:** `FamilySplit.Features.Notifications` created — one query folder (`GetVapidPublicKey`), two command folders (`Subscribe`, `Unsubscribe`), `Data/` (`IPushSubscriptionData` + `internal sealed PushSubscriptionData`), `Shared/` (`NotificationHub`, `VapidPushSender`, `SignalRNotificationService` — the delivery mechanism, not classic use-case handlers), `NotificationsModule`. Built ADR-001-clean from the start. **`GetVapidPublicKey` is a Query with no `AppDbContext`:** it reads only `IConfiguration`, so — per the brief — it's still classified as a Query (pure data access, no business rules, `AllowAnonymous`) but has no data dependency at all; its unit test lives in `UnitTests` (not Testcontainers) since there is nothing to test against a real Postgres. **The `IPushSubscriptionData` design (the brief's "one hard design problem"):** `NotificationHub` previously took `AppDbContext` directly to resolve the connecting user's family on connect — once `FamilySplit.Features.Notifications` joined `FeatureAssemblies.All`, architecture Rule 5 would fail it ("Hub" isn't a sanctioned `AppDbContext`-injecting suffix). Rather than invent a new guard interface, `IPushSubscriptionData` gained `GetActiveFamilyIdForUserAsync(userId, ct)` for the hub's connect-resolution, alongside the Subscribe/Unsubscribe persistence and the reads `VapidPushSender` needs (`GetActiveUserIdsForFamilyAsync`, `GetSubscriptionsForUsersAsync`, `RemoveStaleSubscriptionsAsync`) — mirroring how Admin folded `RequireGlobalAdminAsync` into `IAdminData.IsGlobalAdminAsync` and Families folded caller-resolution into `IFamilyData.GetCallerMemberAsync`, per the brief's explicit precedent. This makes `NotificationHub` (SignalR hub, still not sealed/`*Handler`-named — Rule 3 doesn't apply to it) and the renamed `VapidPushSender` (see below) fully mockable with zero EF anywhere outside `Data/`. **Rename:** `PushNotificationService` was split three ways — `GetVapidPublicKey()` → `GetVapidPublicKeyQueryHandler`, `SubscribeAsync`/`UnsubscribeAsync` (+ the private `ValidatePushField`) → the `Subscribe`/`Unsubscribe` command handlers + a new `SubscribeCommandValidator`, and the remaining `SendToFamilyAsync` VAPID-delivery method → renamed to **`VapidPushSender`** (ctor: `IPushSubscriptionData` + `IConfiguration` + `ILogger`, no `AppDbContext`) since "PushNotificationService" no longer described a single cohesive responsibility. `SignalRNotificationService`'s `IServiceScopeFactory`-based fresh-DI-scope background-push pattern (`DeliverPushAsync`, deliberately not propagating the request's `CancellationToken`) is preserved byte-for-byte, just resolving `VapidPushSender` instead of `PushNotificationService` from the scope. **Validator parity:** `SubscribeCommandValidator` ports `ValidatePushField` 1:1 (endpoint required/≤2048 chars/absolute-https; p256dh/auth required/≤512 chars) — while writing it, a real FluentValidation pitfall surfaced: chaining `.Must(...).WithMessage(...).When(cond)` after `NotEmpty()`/`MaximumLength()` on the *same* `RuleFor` applies `When`'s condition to the **entire chain** by default (`ApplyConditionTo.AllValidatorsInCurrentChain`), which would have silently suppressed the "Endpoint is required" error for a blank endpoint (the `When` condition is false for blank/oversized input, so `NotEmpty` never actually ran). Fixed by splitting the https-URL check into its own second `RuleFor(x => x.Endpoint)` so `When` scopes to only the `Must` validator — covered by a dedicated `EmptyEndpoint_FailsWithMessage` validator test that would have caught the regression. `UnsubscribeCommand` has no validator (matches the legacy no-validation-on-unsubscribe behaviour); `IPushSubscriptionData.RemoveSubscriptionAsync` returns a `bool` so the command handler only logs "removed" when a row actually existed, preserving that log's exact prior conditionality. **DELETE + body gotcha:** `MapDelete` (unlike `MapPost`/`MapPut`) does not infer a complex-type parameter as the request body by default — the original `PushEndpoints.cs` used `[FromBody]` explicitly on unsubscribe for this reason, and the port initially dropped it (compiling fine, but throwing `InvalidOperationException: Body was inferred but the method does not allow inferred body parameters` at endpoint-mapping time, caught immediately by `NotificationsEndpointsTests`); restored the explicit `[FromBody]` on `UnsubscribeEndpoint`. Wired into slnx (`/src/Features/`) / Dockerfile COPY / Api csproj / `Program.cs` (`new NotificationsModule()` appended to the module array; the `AddSignalR()` + `INotificationService` registration, `app.MapPushEndpoints()`, and `app.MapHub<NotificationHub>(...)` calls removed — the `AddAuthentication().AddJwtBearer(...)` block, including its `/hubs` SignalR-querystring-token handling, was **left untouched** per the brief, since Phase 12 (Auth) owns it in a separate worktree) / `FeatureAssemblies.All` / UnitTests csproj. Deleted `Application/Push/` (service), `Api/Hubs/` (both files), `Api/Endpoints/PushEndpoints.cs`, the `PushNotificationService` DI line + its `DependencyInjectionTests` assertion, and the `Lib.Net.Http.WebPush` `PackageReference` from `FamilySplit.Application.csproj` (moved to the new slice's csproj). **Tests:** command-handler unit tests (Moq over `IPushSubscriptionData`, no DB) for `Subscribe`/`Unsubscribe`; `SubscribeCommandValidatorTests` (ports every `ValidatePushField` case); `GetVapidPublicKeyQueryHandlerTests` (plain unit test, Moq over `IConfiguration`, no DB — the query has no data-access layer to speak of); `NotificationHubTests` and `SignalRNotificationServiceTests` ported into `Features/Notifications/Shared/` (now mocking `IPushSubscriptionData` instead of an InMemory `AppDbContext`); new `VapidPushSenderTests` covering the early-return/config-gate branches the legacy `SendToFamilyAsync` tests covered; `NotificationsEndpointsTests` per-module DI/route test (mirrors `GroupsEndpointsTests`/`UsersEndpointsTests` — registrations, the 3 `/push` routes + verbs, `AllowAnonymous` on vapid-public-key, the hub route, display names). Old `Push/PushNotificationServiceTests.cs`, `Hubs/NotificationHubTests.cs`, `Hubs/SignalRNotificationServiceTests.cs`, and `Endpoints/PushEndpointsTests.cs` deleted in the same commit. New Testcontainers coverage added at `IntegrationTests/Notifications/NotificationsEndpointsTests.cs` (no push integration tests existed before this phase) covering: vapid-public-key configured → 200 anonymous (via a per-test `Factory.WithWebHostBuilder` override, since no test previously configured `Push:Vapid:*`) and not-configured → **500** (the legacy uncaught-`InvalidOperationException` behaviour is deliberately preserved, not "fixed" to a handled error); subscribe happy path + upsert-by-endpoint + the https-URL/empty-endpoint 422s + 401-unauthenticated; unsubscribe happy path (row removed, verified via the shared connection) + no-match (still 204) + 401-unauthenticated. **Client:** verified — **zero changes needed**. `IPushClient` (`GetVapidPublicKeyAsync` → `Task<VapidPublicKeyResponse>`, `SubscribeAsync`/`UnsubscribeAsync` → `Task`) already matched the unchanged 200/204/204 wire shapes exactly (Subscribe/Unsubscribe were already `204`-returning before this phase — no strict-CQRS wire-format change here, unlike the Create-type commands in other slices); `PushNotificationClientServiceTests`/`NotificationHubConnectionTests`/`IPushClientTests` all still pass untouched. **Latent bug noticed but out of scope:** `PushSubscriptionConfiguration` caps `p256dh`/`auth` DB columns at 256/128 chars while the validator (ported 1:1 from the legacy code) allows up to 512 — a value in the 129–512/257–512 range would pass validation but could fail at the DB layer. Pre-existing before this phase; flagged as a follow-up task rather than fixed here (not part of the migration's scope, and changing validator/DB limits is a product decision, not a mechanical port). **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **711 unit** (incl. architecture suite over the Notifications assembly — Rules 5/8/10/11 confirm `NotificationHub`/`VapidPushSender`/the command handlers hold no `AppDbContext`/`SaveChanges` and `PushSubscriptionData` is the only write-side EF), **422 client** green (unchanged, confirming the zero-client-impact claim). Integration tests compile clean but were **not executed locally** (Docker/Testcontainers unavailable in the dev environment) — they run in CI.

### ✅ Phase 12 — Auth (DONE)
`Login`, `Callback`, `Refresh`, `Logout` (endpoint lambdas stay thicker — cookie/redirect orchestration is HTTP-edge logic; response shapes unchanged). Auth is a token-exchange protocol, not domain CQRS, so it has **no `I{Slice}Data`** — but `RefreshTokenService` remains its data-access component (issue/rotate/revoke against `refresh_tokens`) and is Testcontainers-tested; `OAuthHandler` orchestration is integration-tested at the seam (Phase 14 smoke tests). `Shared/`: `JwtFactory`, `PkceFlow`, `OAuthHandler` (from `Api/Auth/`), `RefreshTokenService` (from Application), `AuthCookies` helpers. `AuthModule.RegisterServices` takes over from Program.cs: Jwt section read + signing-key check + `AddAuthentication().AddJwtBearer(...)` (incl. SignalR `?access_token=` plumbing via `HubPaths.Prefix`), `JwtFactory`/`PkceFlow` singletons + `OAuthHandler`/`RefreshTokenService` scoped, the `"google-oauth"` HttpClient + resilience, and the **"auth" rate-limit policy** via a second `AddRateLimiter` (options delegates compose; host keeps the global limiter + OnRejected). Auth group keeps `.AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth)`. Api csproj drops `Microsoft.AspNetCore.Authentication.JwtBearer` + `Microsoft.Extensions.Http.Resilience` (move to the slice). Host keeps `AddAuthorization` fallback policy + DataProtection.

**Completion notes:** `FamilySplit.Features.Auth` created — no use-case folders (Auth has no Command/Query handlers by design; Rules 3/4/7/8/10/11 are vacuous here, same as for any query-only slice), just `Shared/` (`JwtFactory`, `PkceFlow` — no `AppDbContext`, singletons, unchanged logic), `Data/` (`OAuthData`, `RefreshTokenData` — both `internal sealed`), `Endpoints/AuthEndpoints.cs` (the four route lambdas, functionally identical to the old `Api/Endpoints/AuthEndpoints.cs`, just resolving the renamed types and `RateLimitPolicies.Auth`/`HubPaths.Prefix` constants instead of literals), and `AuthModule`. **Rule 5 resolution (the brief's hard design problem):** both `OAuthHandler` and `RefreshTokenService` constructor-inject `AppDbContext` and don't match any sanctioned suffix under the as-written Rule 5 — renamed to `OAuthData` and `RefreshTokenData` (method/type names inside otherwise untouched, including the intentionally-not-refactored read-decide-write interleaving in `RefreshTokenData.RotateAsync`). Went with the brief's recommended fix rather than widening Rule 5's suffix allow-list, for the same reason given there: a generic `"Handler"` suffix would also silently admit any future `*CommandHandler`. **`AuthModule.RegisterServices` design note:** `IFeatureModule.RegisterServices(IServiceCollection, IConfiguration)` has no `IWebHostEnvironment` parameter, but `RequireHttpsMetadata` needs to know the environment. Resolved by registering `JwtBearerOptions` via `services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IWebHostEnvironment>((options, env) => ...)` instead of the `AddJwtBearer(options => ...)` delegate directly — `IWebHostEnvironment` is resolved from DI at options-binding time instead of module-registration time, so no `IFeatureModule` signature change was needed. **Rate-limiter-policy composition — verified empirically, works as documented:** `AuthModule.RegisterServices` calls its own `services.AddRateLimiter(options => options.AddPolicy(RateLimitPolicies.Auth, ...))`; the host's separate `AddRateLimiter` call (kept in `Program.cs`) still owns `GlobalLimiter`/`RejectionStatusCode`/`OnRejected`. Both configure-delegates run against the same `RateLimiterOptions` instance (ASP.NET Core's options infrastructure runs every registered `IConfigureOptions<RateLimiterOptions>` in registration order), so the "auth" named policy from the slice and the global limiter from the host coexist — confirmed via `AuthEndpointsTests.RegisterServices_ComposesWithHostsOwnAddRateLimiterCall_AndRegistersAuthPolicy` (which registers both `AddRateLimiter` calls into one `ServiceCollection` exactly as `Program.cs` does, then proves both survived: the host's `RejectionStatusCode`/delegate-execution stuck, and re-adding the `"auth"` policy name throws `ArgumentException("...already exists...")`, which is only possible if `AuthModule`'s `AddPolicy` call had already registered it) and, at the full-host level, the pre-existing `RefreshRotationTests`/`LogoutTests` (unauthenticated-but-not-429 requests hitting `/auth/refresh` and `/auth/logout` through the real pipeline) continuing to pass unmodified. Wired into slnx (`/src/Features/`) / Dockerfile COPY / Api csproj (also moved the `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.Extensions.Http.Resilience` `PackageReference`s from the Api csproj to the slice csproj) / `Program.cs` (`new AuthModule()` added to the module array; the inline JwtBearer/HttpClient/rate-limit-policy/JwtFactory-PkceFlow-OAuthHandler registration blocks and the `MapAuthEndpoints()` call removed — the host keeps only `AddAuthorization` fallback policy and `AddDataProtection().PersistKeysToDbContext<AppDbContext>()`, neither of which is Auth-specific; `AddSignalR`/`INotificationService` had already moved into `NotificationsModule` by Phase 11) / `FeatureAssemblies.All`. Deleted `Api/Auth/` (`JwtFactory.cs`, `PkceFlow.cs`, `OAuthHandler.cs`), `Application/Auth/` (`RefreshTokenService.cs`), `Api/Endpoints/AuthEndpoints.cs`, and the `Application` DI line for `RefreshTokenService` (left an explanatory comment matching the style of the other migrated-slice comments already in that file). **Tests:** `PkceFlowTests`/`JwtFactoryTests` ported unchanged (pure, no DB) into `Features/Auth/Shared/`; `OAuthHandlerTests`/`RefreshTokenServiceTests` (EF InMemory, ~37 cases combined) **deleted without a direct replacement** — per the plan, their data-access coverage is superseded by the pre-existing Testcontainers `IntegrationTests/Auth/AuthEndpointsTests.cs` (`RefreshRotationTests`, `RefreshTheftDetectionTests`, `RefreshSecurityTests`, `LogoutTests`, 13 scenarios covering rotation, reuse-window, theft detection including the concurrent-retry vs. genuine-replay distinction, plaintext-never-stored, and logout) which needed **zero behavioral changes**, only continuing to compile against the new module (verified by a clean `dotnet build tests/FamilySplit.IntegrationTests`); `OAuthData`'s Google-HTTP-mocking scenarios (token exchange, userinfo fetch, FamilyMember linking/rejection edge cases) are **not** re-covered by a new Testcontainers test in this phase — reaching `OAuthData` requires mocking Google's HTTP endpoints through the full `/auth/callback/Google` pipeline, which the plan explicitly defers to Phase 14's integration-at-the-seam smoke tests; this is a known, deliberate coverage gap carried forward from the plan's own text, not an oversight. New `Endpoints/AuthEndpointsTests.cs` (per-module DI/route test, 17 cases: signing-key validation (2), JwtFactory/PkceFlow/OAuthData/RefreshTokenData registration + lifetimes (4), JwtBearer scheme registration, google-oauth HttpClient registration, the rate-limiter-composition proof above, and the 4 routes' verb/pattern/AllowAnonymous/tag/DisplayName (8)) replaces the old `MapAuthEndpoints`-only shape test; `DependencyInjectionTests` `RefreshTokenService` assertion removed. Old `tests/FamilySplit.UnitTests/Auth/` (`OAuthHandlerTests.cs`, `RefreshTokenServiceTests.cs`, `PkceFlowTests.cs`, `JwtFactoryTests.cs`) and `tests/FamilySplit.UnitTests/Endpoints/AuthEndpointsTests.cs` deleted in the same commit. **Client:** untouched — `IAuthApi`/`AuthService` wire formats are unchanged, confirmed by the full Client.UnitTests suite staying green. **Verification:** Release build 0 warnings/0 errors (full `FamilySplit.slnx`); **654 unit** tests green (incl. the architecture suite over the new Auth assembly — Rules 5/8/10/11 are vacuous for Auth since it has no Command/Query handlers, confirming the rename resolved the only rule that would otherwise fire); **422 client** tests green (unaffected, as expected); `dotnet build tests/FamilySplit.IntegrationTests -c Release` succeeds 0/0 (Testcontainers/Docker unavailable locally, so the Auth integration suite itself runs in CI only, per environment constraints).

### Phase 13 — Delete `FamilySplit.Application`  
Remove: project folder, slnx entry, Dockerfile COPY line, `AddFamilySplitApplication()` call, ProjectReferences in Api/UnitTests/IntegrationTests, remaining `DependencyInjectionTests` assertions for it.

### Phase 14 — Hardening, docs, CI  
- [ ] Integration smoke tests for the seams: `POST /auth/refresh` returns non-500 (proves composed "auth" rate-limit policy exists); `POST /hubs/notifications/negotiate` returns 401-not-404 (proves module-mapped hub); dev OpenAPI document lists module endpoints.
- [ ] `dotnet format FamilySplit.slnx --verify-no-changes` (CI format gate).
- [ ] Update **CLAUDE.md** (solution structure, conventions, validation/logging sections all reference the old layering; document the CQRS command/query rules and response semantics) + `.github/copilot-instructions.md`. *(Done as of the 2026-06-16 revision: both files now carry a "Vertical Slice Architecture" section documenting the ADR-001 business-logic/data-access seam and the Testcontainers-vs-Moq test split; finalise the per-section sweep here once the legacy `Application` layer is gone.)*
- [ ] Full E2E pass locally (publish client wwwroot per CI recipe) — the flows exercise every re-query path through the real UI.
- [ ] Push branch; full CI green before merging (deploy auto-fires on CI success on `main`).

---

## Risks / guardrails

| Risk | Guardrail |
|---|---|
| **Wire-format change (strict CQRS responses)** breaks client/integration tests silently | Change is made slice-by-slice with client effects + integration tests updated in the *same commit*; E2E flows (pilot + Phase 14) prove the re-query path through the real UI |
| Service-test splits (7 big files) | TestBase extraction + region-comment cuts + test-count parity gate |
| Admin/Families DTO duplication drifts query wire format | Query-side integration tests assert raw JSON, run unmodified every phase |
| `.RequireRateLimiting("auth")` fails at request time if policy missing | `RateLimitPolicies.Auth` constant both sides + Phase 14 smoke test |
| JwtBearer ↔ hub path hidden coupling | `HubPaths` constants in Common |
| Half-migrated commits | `TreatWarningsAsErrors=true` fails loudly; one slice per commit |
| Captive dependencies / missing registrations | `ValidateOnBuild` + `ValidateScopes` already on; handlers scoped like today's services |
| Docker breaks mid-migration | Each phase adds its Dockerfile COPY line in the same commit |
| 422-for-not-found "fixed" to 404 | Forbidden — preserve `ValidationErrors.NotFound` semantics |
| Extra HTTP round-trip after each mutation (UX latency) | Accepted trade-off of strict CQRS; effects dispatch existing Load actions which already show loading states |
| **Tracked entities leak through `I{Slice}Data`** (re-couples business logic to EF, breaks mockability) | Reads return plain records/DTOs only — never `IQueryable`/tracked entities; Rule 8 forbids `AppDbContext`/`DbSet<>` in command handlers |
| **Business logic drifts back into `{Slice}Data`** (fat gateway → untestable rules) | Gateway methods are mechanical persist/read only; decisions/calculators stay in the handler; reviewed per slice |
| **Audit no longer atomic** once `SaveChanges` moves to the gateway | Gateway's persist method flushes `AuditService.Queue` in the *same* `SaveChangesAsync` — assert atomicity in the `{Slice}Data` Testcontainers test |
| Command unit tests silently revert to a DB provider | Rule 11 (required) fails any command-handler test referencing `AppDbContext`/`DbContextOptions`/`UseInMemoryDatabase` |
| Two already-merged slices stuck on the old shape | Explicit Retrofit callouts in Phase 3/4; do them before Phase 5 so all live slices share one model |

## Verification commands

```bash
dotnet build FamilySplit.slnx -c Release
dotnet test tests/FamilySplit.UnitTests/FamilySplit.UnitTests.csproj -c Release --no-build
dotnet test tests/FamilySplit.IntegrationTests/FamilySplit.IntegrationTests.csproj -c Release --no-build --filter "Category=Integration"   # needs Docker
dotnet test tests/FamilySplit.Client.UnitTests/FamilySplit.Client.UnitTests.csproj -c Release --no-build
# E2E (after pilot + at the end): publish client, set E2E_CLIENT_WWWROOT, then
dotnet test tests/FamilySplit.E2ETests/FamilySplit.E2ETests.csproj -c Release --filter "Category=E2E"
```
