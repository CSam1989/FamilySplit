# Vertical Slice Architecture Refactor — Phased Plan

> Execution tracker for restructuring the FamilySplit backend into vertical slices with a
> CQRS split inside each slice. Each phase is one green commit: `dotnet build FamilySplit.slnx -c Release`
> + UnitTests + IntegrationTests (+ Client.UnitTests for phases that touch the client) must pass
> before committing. Phases are designed to be executed independently (one per session is fine) —
> state is always releasable between phases.
>
> Branch: `refactor/vertical-slices`. Baseline (commit `7034697`): 688 unit / 431 client / 96 integration tests green.

---

## Decisions (binding, user-confirmed)

1. **One class-library project per feature slice** under `src/Features/`, grouped in a `/src/Features/` solution folder. Thin API host keeps the name `FamilySplit.Api` (E2E `dotnet run` + Dockerfile depend on it).
2. **`FamilySplit.Domain` + `FamilySplit.Infrastructure` stay unchanged** (entities, enums, AppDbContext, EF configs, migrations).
3. **Use-case folders inside each slice, split CQRS-style** (e.g. `Expenses/Create/`, `Expenses/GetDetail/`). Service classes dissolve into one handler per use case. **No MediatR.**
   - **Queries** (reads) are the data-access layer: thin handlers doing EF projections — `AsNoTracking()`, no mutations, no business rules (authorization guards still apply).
   - **Commands** (writes) hold the business logic: validation → guards → rules → mutations → `SaveChangesAsync` → audit/notify.
4. **Strict CQRS command responses (BREAKING wire-format change, chosen deliberately):** commands no longer return detail DTOs. Creates return `201 Created` + `Location` + `{ "id": "<guid>" }`; all other mutations return `204 No Content`. The client re-queries after mutating. See the response-semantics table below for the per-endpoint mapping and the two documented exceptions (Join, RegenerateInviteCode).
5. **Per-slice DTOs and logic.** Only *truly shared* code goes in `FamilySplit.Common`. Slice-local: `SplitCalculator`+`ExpenseReshuffleRequired`→Expenses; `BalanceCalculator`+`SettlementOptimiser`+`SettlementStateMachine`→Settlements; `ParticipantSeeder`+`ActivityCloseGuard`→Activities. Common: `WeightCalculator` (4 slices), `AuditService` (mechanism only — slices own what they audit), `ForbiddenException`, `CreatedResponse`.
6. **Consistency enforcement:** `IFeatureModule` pattern + NetArchTest architecture tests (including CQRS rules).
7. **Keep the 4 test projects** (CI paths untouched); folders mirror slices.
8. **Blazor Client scope (revised by decision 4):** the client's internal architecture stays out of scope, but each slice phase must update the client's Refit mutation signatures, Fluxor effects (re-query after mutate), affected client DTOs, and their client unit tests — otherwise the app breaks against the new wire format.

## Verified facts

- `Application/FamilyMembers/*` are tombstones; `GET /users/me/profile` is backed by `FamilyService` → **no FamilyMembers slice**, Families absorbs it. `GroupMembersEndpoints` is a no-op stub → delete.
- `Api/Middleware/ForbiddenException.cs` is blank; real one is `Application/Exceptions/ForbiddenException.cs`.
- `RequireGroupMemberAsync` is duplicated **verbatim** in ActivityService:367, ExpenseService:388, SettlementService:533; caller-family lookup also in DashboardService and SettlementService:548 → `GroupMembershipGuard` in Common. NotificationHub keeps its inline non-throwing lookup (silent no-op, not 403 — do not change).
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
    <Using Include="FamilySplit.Common.Security" /> <!-- GetUserId() without per-file usings -->
  </ItemGroup>
</Project>
```

### CQRS classification and naming

| | Query use case | Command use case |
|---|---|---|
| Purpose | Pure data access (the "access layer") | Business logic + mutations |
| Class names | `{UseCase}Query` (record, only if there are non-route inputs) + `{UseCase}QueryHandler` | `{UseCase}Command` (record, only if there is a request body) + `{UseCase}CommandHandler` |
| EF usage | `AsNoTracking()` projections; **never** `SaveChangesAsync`, never entity mutation | Tracked entities, `SaveChangesAsync` |
| Allowed deps | `AppDbContext`, `GroupMembershipGuard` (authz is not business logic), mappers | + validators, `AuditService`, `INotificationService`, slice guards/calculators |
| Validation | None today (route-bound ids only) | `await _validator.ValidateAndThrowAsync(cmd, ct)` first statement when a body exists |
| Returns | DTOs (same shapes as today — read side is the wire-format lock) | `Guid` id for creates, nothing for everything else |

- Validators are named `{UseCase}CommandValidator` and live in the command's folder (they validate the `{UseCase}Command` record).
- Route-bound parameters (groupId, activityId, callerId) pass directly into `HandleAsync(...)` — don't wrap them in records.
- Response DTOs live **in the query's use-case folder** (e.g. `GetDetail/ExpenseDetailDto.cs`); they move to slice `Shared/` only when a second use case in the same slice needs them (e.g. `ExpenseParticipantDto` used by List + GetDetail).
- Because commands no longer return detail DTOs, the old service `BuildDetailDtoAsync` helpers collapse **into the GetDetail query handler** — most planned `Shared/*Reader` classes disappear.

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
// QUERY — thin data access, no business rules
public sealed class GetExpenseDetailQueryHandler
{
    // ctor-inject: AppDbContext, GroupMembershipGuard, ILogger<T>
    public async Task<ExpenseDetailDto> HandleAsync(Guid expenseId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Fetching expense {ExpenseId} for user {UserId}", expenseId, callerId);
        // authz guard, then AsNoTracking projection (body absorbed from BuildDetailDtoAsync);
        // ValidationErrors.NotFound(...) when missing (422 semantics preserved)
    }
}

// COMMAND — business logic; returns the new id (creates) or nothing
public sealed class CreateExpenseCommandHandler
{
    // ctor-inject: AppDbContext, CreateExpenseCommandValidator, GroupMembershipGuard,
    //              AuditService, slice calculators, ILogger<T>
    public async Task<Guid> HandleAsync(
        Guid activityId, CreateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating expense on activity {ActivityId} by user {UserId}", activityId, callerId);
        await _validator.ValidateAndThrowAsync(cmd, ct);   // first statement
        // ... guards, business rules, mutations (body copied from the service method,
        //     minus the final detail-DTO read) ...
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Expense {ExpenseId} created ... by user {UserId}", ...);
        return expense.Id;
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

Rules: handlers sealed, registered scoped, namespace `FamilySplit.Features.{Slice}.{UseCase}`. Endpoint classes never touch `AppDbContext`. Happy path at the bottom, early returns for guards (existing repo convention). Logging: `LogDebug` entry, `LogInformation` on successful mutation (commands only).

### Client update recipe (per slice with mutations)

1. **Refit interface** (`Client/Services/I{X}Client.cs`): creates return `Task<CreatedResponse>`, other mutations return `Task` — remove the old DTO return types. Add a client-side `CreatedResponse` record once (client DTOs stay duplicated by design).
2. **Fluxor effects**: after a successful mutation, dispatch the existing Load action(s) instead of consuming a returned DTO (e.g. `CreateExpenseAction` effect → `await client.Create(...)` → dispatch `LoadExpensesAction(...)`; Join group → navigate using the returned id, then `LoadGroupsAction`). Reducers that patched state from command responses now rely on the re-query.
3. **Client unit tests**: update the affected `Effects*Tests` (mock setup + dispatched-action assertions) and Refit-contract tests.
4. UI pages/components need no edits unless they read a command response directly (audit while migrating the slice).

### Test conventions

- Per slice: `tests/FamilySplit.UnitTests/Features/{Slice}/{UseCase}/{UseCase}CommandHandlerTests.cs` / `{UseCase}QueryHandlerTests.cs` (+ `{UseCase}CommandValidatorTests.cs` beside it).
- Extract `{Slice}TestBase` from the old service-test constructor (InMemory DbContext + seed helpers, moved verbatim).
- Old service-test files are sectioned with `// ── MethodAsync ──` region comments — those are the exact cut points. Method rename `CreateAsync_X_Y` → `Handle_X_Y`. Read-side assertions unchanged; command-side assertions change from inspecting the returned DTO to asserting the returned id / DB state (query the InMemory context).
- **Parity gate:** new per-handler test count >= old file count (minus deliberately dropped per-class constructor tests). Delete the old file in the same commit.
- Endpoint tests (`Endpoints/*Tests.cs`) keep their literal route-pattern asserts; their `CreateApp()` swaps service registration for `new XModule().RegisterServices(...)` / `MapEndpoints(...)` — they become the per-module DI tests.
- IntegrationTests: query-endpoint tests untouched; mutation-endpoint tests updated in the same slice phase to assert `201 + Location + {id}` / `204` and to follow up with a GET where the old test asserted response-body content.

### Per-slice phase checklist (the same steps every time)

1. Create `src/Features/FamilySplit.Features.{X}/` project (template above).
2. Add to `FamilySplit.slnx` under `/src/Features/` solution folder.
3. Add COPY line to `src/FamilySplit.Api/Dockerfile` restore block.
4. Api csproj: add ProjectReference; Program.cs: add `new {X}Module()` to the array; remove the old `Map{X}Endpoints()` call.
5. Classify each use case **Command or Query** (inventory below); create use-case folders; dissolve the service into handlers (copy bodies; queries get `AsNoTracking`; commands drop the final detail-read and return ids/nothing; swap `NotFound()`/`Throw422()` → `ValidationErrors`; swap private guards → `GroupMembershipGuard` where applicable); move slice-local calculators/guards into `Shared/`; DTOs into their query folders (or `Shared/` if multi-use-case).
6. Delete the old `Api/Endpoints/{X}Endpoints.cs`, the `Application/{X}/` folder, and the slice's lines in `Application/DependencyInjection.cs`.
7. UnitTests: add ProjectReference to the new feature project; migrate tests per the recipe; delete old files same commit.
8. IntegrationTests: update this slice's **mutation** tests to the new response shapes (+ follow-up GETs); leave query tests untouched.
9. Client: apply the client update recipe (Refit signatures, effects re-query, client unit tests).
10. Append the slice assembly to `tests/FamilySplit.UnitTests/Architecture/FeatureAssemblies.cs`.
11. Verify: `dotnet build FamilySplit.slnx -c Release` && UnitTests && IntegrationTests && Client.UnitTests green.
12. Commit (one commit per slice).

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

### ✅ Phase 4 — Dashboard (DONE)
`GetStats` (Query — single handler from `DashboardService`). No mutations → no client/integration-test changes. Smallest slice — fast confirmation of the pattern.

**Completion notes:** `FamilySplit.Features.Dashboard` created (single `GetStats/` use-case folder: `GetStatsQueryHandler` + `DashboardGroupStatDto` + `GetStatsEndpoint`, plus `DashboardModule`). csproj omits FluentValidation (no command/body → no validators), so `RegisterServices` registers only the one handler and does **not** call `AddValidatorsFromAssembly`. Handler body copied verbatim from `DashboardService.GetStatsAsync`, with `ILogger<GetStatsQueryHandler>` injected (LogDebug entry) and `.AsNoTracking()` added to each entity-rooted query (read-side intent). Route unchanged (`GET /dashboard/stats` → `Results.Ok(stats)`), so the read-side wire format is preserved — client DTO + effects + reducers untouched. Wired into slnx (`/src/Features/`), Dockerfile COPY, Api csproj, and `Program.cs` (`new DashboardModule()` in the module array; `app.MapDashboardEndpoints()` removed). Old `Application/Dashboard/` (service + DTOs), `Api/Endpoints/DashboardEndpoints.cs`, and the Application-DI line deleted. Tests: `DashboardServiceTests` (11) → `Features/Dashboard/GetStats/GetStatsQueryHandlerTests` (11, `GetStatsAsync_*`→`Handle_*`, self-contained — no separate TestBase needed for a single handler); `DashboardEndpointsTests` rewritten as a per-module DI/route test (2→4); `DependencyInjectionTests` DashboardService assertion removed; `FeatureAssemblies.All` now registers the Dashboard assembly so all 11 architecture rules apply. **Verification:** Release build 0 warnings/0 errors; 701 unit (was 699) / 431 client green. No Dashboard integration tests exist (query-only, unchanged route) and the client is untouched, so nothing else to run.

### Phase 5 — Users  
`WhoAmI` (Query — inline `db.Users` projection from `UserEndpoints.cs`). No mutations.

### Phase 6 — Groups  
Queries: `List`, `GetDetail`. Commands: `Create` (**201+id**), `Update` (**204**), `Join` (**200+`{id}` — documented exception**), `Leave` (204), `RegenerateInviteCode` (**verify**: 204 + re-query if `GroupDetailDto` exposes the code, else `200+{inviteCode}`).
`Shared/`: group-admin guard, invite-code helper, member-mapping DTOs (uses Common `WeightCalculator`). Delete `GroupMembersEndpoints.cs` stub. Client: `IGroupClient` + Group effects (Join navigates via returned id, then re-queries).

### Phase 7 — Activities  
Queries: `List`, `GetDetail`. Commands: `Create` (**201+id**), `CreateSubActivity` (**201+id**), `Update` (204), `Close` (204), `AddParticipant` (204), `RemoveParticipant` (204).
`Shared/`: `ParticipantSeeder`, `ActivityCloseGuard`, participant DTOs (detail builder collapses into GetDetail query). Move `Core/ParticipantSeederTests.cs` + close-guard section of `BusinessGuardTests` along. Client: `IActivityClient` + Activity effects.

### Phase 8 — Settlements  
Queries: `GetBalances`, `List`, `GetDetail`, `ListForGroup`, `ListMyPending`. Commands: `Generate` (**204** — idempotent, client re-queries the list; was: summary list), `ConfirmSent` (**204**, was detail DTO), `ConfirmReceived` (**204**).
`Shared/`: `BalanceCalculator`, `SettlementOptimiser`, `SettlementStateMachine`, `SettlementQueryHelpers` (LoadExpenseData/GetActivityCurrency/GetActivityAndSubIds — used by both queries and Generate), summary DTOs. Consumes Common `INotificationService` (implementation still host-registered until Phase 11 — fine, bound by interface). Move calculator/state-machine tests along. Largest test split (991 lines, 8 handlers). Client: `ISettlementClient` + Settlement effects (mark-sent/mark-received re-query settlements + balances).

### Phase 9 — Admin (MUST precede Families)  
Queries: `ListFamilies`, `GetFamily`. Commands: `CreateFamily` (**201+id**), `AddFamilyMember` (**201+id**), `UpdateFamilyMember` (204), `RemoveFamilyMember` (204), `DeleteGroup` (204), `AddFamilyToGroup` (204), `RemoveFamilyFromGroup` (204).
`Shared/`: `RequireGlobalAdmin` guard + **own copies** of FamilyDto/FamilyMemberDto + member validators (identical JSON property names on the query side — `IntegrationTests/Admin` query tests lock it). Client: `IAdminClient` + Admin effects.

### Phase 10 — Families  
Queries: `GetMyFamily`, **`GetMyProfile`** (absorbs `GET /users/me/profile`; maps its own `/users/me` group). Commands: `UpdateFamilyName` (204), `AddMember` (**201+id**), `UpdateMember` (204), `RemoveMember` (204).
`Shared/`: family-admin guard, member mapper (uses Common `WeightCalculator`), shared member DTOs. Delete `FamilyEndpoints.cs`, `FamilyMembersEndpoints.cs`, `Application/Families/`, and the tombstone `Application/FamilyMembers/`. Client: `IFamilyClient` + Family effects.

### Phase 11 — Notifications  
Query: `GetVapidPublicKey` (AllowAnonymous). Commands: `Subscribe` (204), `Unsubscribe` (204).
Plus `PushNotificationService`, `NotificationHub`, `SignalRNotificationService` (keep the `IServiceScopeFactory` background-push pattern exactly). Module: `AddSignalR()` + `AddScoped<INotificationService, SignalRNotificationService>()` + maps `/push` group and `MapHub<NotificationHub>(HubPaths.Notifications)`. Host drops `Hubs/` folder, `AddSignalR`, the INotificationService registration, and `MapHub`. `Lib.Net.Http.WebPush` package moves from Application csproj to this slice. Client: `IPushClient` signatures if response shapes change.

### Phase 12 — Auth (last; most host-entangled — outside CQRS response rules)  
`Login`, `Callback`, `Refresh`, `Logout` (endpoint lambdas stay thicker — cookie/redirect orchestration is HTTP-edge logic; response shapes unchanged). `Shared/`: `JwtFactory`, `PkceFlow`, `OAuthHandler` (from `Api/Auth/`), `RefreshTokenService` (from Application), `AuthCookies` helpers. `AuthModule.RegisterServices` takes over from Program.cs: Jwt section read + signing-key check + `AddAuthentication().AddJwtBearer(...)` (incl. SignalR `?access_token=` plumbing via `HubPaths.Prefix`), `JwtFactory`/`PkceFlow` singletons + `OAuthHandler`/`RefreshTokenService` scoped, the `"google-oauth"` HttpClient + resilience, and the **"auth" rate-limit policy** via a second `AddRateLimiter` (options delegates compose; host keeps the global limiter + OnRejected). Auth group keeps `.AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth)`. Api csproj drops `Microsoft.AspNetCore.Authentication.JwtBearer` + `Microsoft.Extensions.Http.Resilience` (move to the slice). Host keeps `AddAuthorization` fallback policy + DataProtection.

### Phase 13 — Delete `FamilySplit.Application`  
Remove: project folder, slnx entry, Dockerfile COPY line, `AddFamilySplitApplication()` call, ProjectReferences in Api/UnitTests/IntegrationTests, remaining `DependencyInjectionTests` assertions for it.

### Phase 14 — Hardening, docs, CI  
- [ ] Integration smoke tests for the seams: `POST /auth/refresh` returns non-500 (proves composed "auth" rate-limit policy exists); `POST /hubs/notifications/negotiate` returns 401-not-404 (proves module-mapped hub); dev OpenAPI document lists module endpoints.
- [ ] `dotnet format FamilySplit.slnx --verify-no-changes` (CI format gate).
- [ ] Update **CLAUDE.md** (solution structure, conventions, validation/logging sections all reference the old layering; document the CQRS command/query rules and response semantics) + `.github/copilot-instructions.md`.
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

## Verification commands

```bash
dotnet build FamilySplit.slnx -c Release
dotnet test tests/FamilySplit.UnitTests/FamilySplit.UnitTests.csproj -c Release --no-build
dotnet test tests/FamilySplit.IntegrationTests/FamilySplit.IntegrationTests.csproj -c Release --no-build --filter "Category=Integration"   # needs Docker
dotnet test tests/FamilySplit.Client.UnitTests/FamilySplit.Client.UnitTests.csproj -c Release --no-build
# E2E (after pilot + at the end): publish client, set E2E_CLIENT_WWWROOT, then
dotnet test tests/FamilySplit.E2ETests/FamilySplit.E2ETests.csproj -c Release --filter "Category=E2E"
```
