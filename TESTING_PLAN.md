# FamilySplit — Testing Plan

> **Status: COMPLETE** — all 27 tasks finished. See the completion notes at the bottom.
>
> Original plan: build a full automated test suite (unit, client/bUnit, integration, E2E) running in CI.

---

## Decisions (locked in)

| Topic | Decision | Consequence |
|---|---|---|
| Service DB queries | **Keep direct `AppDbContext` queries** (inline LINQ, explicit joins per CLAUDE.md). No repository/query-object refactor. | Unit tests cover **pure logic only**. All DB-touching behaviour is verified by the integration suite. |
| Mocking library | **Moq** (already used everywhere). | CLAUDE.md must be corrected — it currently says NSubstitute. No test rewrites. |
| Integration DB isolation | **Transaction-rollback per test** against **Testcontainers** Postgres. | Requires the shared-open-connection pattern (below) so the API and the test share one transaction. Respawn is the documented fallback if this proves brittle. |
| Project layout | **Split into 4 test projects.** | Existing client store/service tests move out of `FamilySplit.UnitTests` into a new client project. |

### Target project layout

```
tests/
├── FamilySplit.UnitTests            # server pure logic (calculators, validators, mappers, guards)  [EXISTS — trim to server-only]
├── FamilySplit.Client.UnitTests     # bUnit components + Fluxor store tests + FormatHelper          [NEW]
├── FamilySplit.IntegrationTests     # Testcontainers Postgres + WebApplicationFactory               [NEW — referenced by CI but missing]
└── FamilySplit.E2ETests             # Playwright + Testcontainers + running app                      [NEW]
```

CI categories: tag integration tests `[Trait("Category","Integration")]` and E2E `[Trait("Category","E2E")]` so jobs can filter.

---

## Pre-flight facts (verified against the repo on 2026-06-01)

These are real constraints the tasks below depend on — not assumptions:

- **`Program.cs` is not partial.** `WebApplicationFactory<Program>` needs `Program` to be a public type. Add `public partial class Program { }` (Task 2.1).
- **Connection-string key is `Postgres`** (`configuration.GetConnectionString("Postgres")` in `Infrastructure/DependencyInjection.cs`), settable via `ConnectionStrings__Postgres`. ⚠️ The existing `ci.yml` integration job uses `ConnectionStrings__DefaultConnection` — wrong key. Fix in Task 5.1.
- **Existing CI integration job points at a Neon shared branch and is disabled (`if: false`).** We replace it with Testcontainers (Task 5.1).
- **JWT minting helper already exists**: `src/FamilySplit.Api/Auth/JwtFactory.cs` — reuse it to mint caller tokens in integration/E2E setup.
- **Test stack already present**: xUnit v3, FluentAssertions 8, Moq 4.20, EF Core InMemory. Packages are centrally managed in `Directory.Packages.props`.
- **Endpoint groups** (`src/FamilySplit.Api/Endpoints/`): Auth, User, FamilyMembers, Family, Admin, Groups, GroupMembers(stub), Activity, Expense, Settlement, Dashboard, Push.
- **~30 client test files** currently live in `tests/FamilySplit.UnitTests/Store/**` and `/Services/**` — these move to the new client project in Task 3.1.

---

## Dependency graph

```
Phase 0 (foundations)
  0.1 reconcile CLAUDE.md + slnx + packages
        │
        ├──────────────┬───────────────────┬─────────────────────┐
        ▼              ▼                   ▼                     ▼
Phase 1 server unit   Phase 3 client      Phase 2 integration    (Phase 4 E2E
  1.1 split projects     3.1 scaffold        2.1 scaffold base      depends on 2.1's
  1.2 logic audit        3.2 extract comps   2.2–2.6 per group       container/app host)
  1.3 validators         3.3 bUnit tests
  1.4 calculators
        │                    │                   │                     │
        └────────────────────┴───────────────────┴─────────────────────┘
                                     ▼
                          Phase 5 — CI wiring (5.1)
```

Phases 1, 2, 3 can run in parallel once 0.1 lands. Phase 4 needs 2.1 (shared container/host harness). Phase 5 is last.

---

# Phase 0 — Foundations

## Task 0.1 — Reconcile docs, solution, and packages
**Goal:** one source of truth before any test code is written.
**Depends on:** nothing.
**Steps:**
1. Update `CLAUDE.md` Testing section: replace **NSubstitute → Moq**; describe the 4-project layout; document the **transaction-rollback-per-test via shared open connection** isolation strategy and the Respawn fallback.
2. Add the three new (empty) test projects and register all four in `FamilySplit.slnx` under the `/tests/` folder.
3. Add test packages to `Directory.Packages.props` (versions to pin at task time):
   - `Testcontainers.PostgreSql`
   - `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory`)
   - `Npgsql` (explicit, for the shared connection)
   - `bunit`
   - `Microsoft.Playwright` + `Microsoft.Playwright.Xunit`
   - `Respawn` (fallback isolation)
4. Confirm `dotnet restore FamilySplit.slnx` and `dotnet build` succeed with empty projects.

**Acceptance:** solution builds with 4 test projects; CLAUDE.md matches reality (Moq, 4 projects, isolation strategy); no version warnings.

---

# Phase 1 — Server unit tests (pure logic)

> Scope per the locked decision: **no DB**. Test calculators, validators, mappers, status-transition guards, and any pure decision logic. Keep using Moq for `ILogger<T>` etc.

## Task 1.1 — Split client tests into `FamilySplit.Client.UnitTests`
**Goal:** `FamilySplit.UnitTests` becomes server-only; client tests move out.
**Depends on:** 0.1.
**Steps:**
1. Move `tests/FamilySplit.UnitTests/Store/**` and the client-side `Services/**` tests (Refit client, JwtAuthHandler, IncludeCredentialsHandler, AuthService, ErrorHelper, NotificationHub connection, PushNotificationClientService) into `FamilySplit.Client.UnitTests`.
2. Remove the `FamilySplit.Client` project reference from `FamilySplit.UnitTests`; keep Api/Application/Domain/Infrastructure refs.
3. Fix namespaces/`GlobalUsings`. Run both projects green.

**Acceptance:** `FamilySplit.UnitTests` references no client project; all moved tests pass in the new project; no test lost.

## Task 1.2 — Pure-logic audit and extraction
**Goal:** ensure all non-trivial decision logic is in pure, directly-testable methods (DB queries stay in services).
**Depends on:** 1.1.
**Steps:**
1. Audit services for inlined business rules that aren't DB queries: weight-snapshot re-trigger conditions in `ExpenseService.UpdateAsync`, settlement status-transition guards (`Proposed→PayerSent→Completed`), activity close/absorb logic, "all balances zero ⇒ Settled" check.
2. Extract each into a pure static method (e.g., `SettlementStateMachine.CanConfirmSent(status, callerIsPayer)`), keeping the service as a thin caller. Do **not** touch the query code.
3. Add unit tests for every extracted method.

**Acceptance:** every extracted guard/decision has direct unit tests; services unchanged behaviourally (existing tests still green).

## Task 1.3 — Complete validator coverage
**Goal:** every `AbstractValidator<T>` has valid-input + per-rule-violation tests.
**Depends on:** 1.1.
**Steps:** For each validator in `ActivityValidator`, `AdminValidator`, `ExpenseValidator`, `FamilyValidator`, `GroupValidator` (and FamilyMembers): use `TestValidateAsync` + `ShouldHaveValidationErrorFor` / `ShouldNotHaveAnyValidationErrors` for each rule and a happy path.
**Acceptance:** 100% of validator rules exercised; coverage report shows validators fully covered.

## Task 1.4 — Complete core-calculator coverage
**Goal:** exhaustive edge-case coverage of pure math.
**Depends on:** 1.1.
**Steps:** `WeightCalculator` (every tier boundary date incl. the day before/after each age threshold, Override tier); `SplitCalculator` (rounding-remainder to heaviest participant, excluded = 0, single participant, zero amount); `BalanceCalculator` (creditor/debtor signs across mixes); `SettlementOptimiser` (≤ N-1 transfers, zero-balance early exit, multi-family chains); `ParticipantSeeder` logic where pure.
**Acceptance:** boundary dates and rounding paths explicitly asserted; calculators at/near 100% line coverage.

---

# Phase 2 — Integration tests (Testcontainers + transaction rollback)

> The whole server stack: HTTP → endpoint → service → **real Postgres** → response.

## Task 2.1 — Scaffold integration harness (do this first)
**Goal:** a reusable base that boots Postgres once, hosts the API in-process, and gives each test a clean DB via transaction rollback.
**Depends on:** 0.1.
**Steps:**
1. Add `public partial class Program { }` at the end of `src/FamilySplit.Api/Program.cs` so `WebApplicationFactory<Program>` can reference it.
2. **Container fixture** (`ICollectionFixture`): one `PostgreSqlContainer` (`postgres:16-alpine`) per test collection; apply EF migrations on start.
3. **Shared-connection transaction pattern** (this is what makes rollback-per-test actually work across the HTTP boundary):
   - In a custom `WebApplicationFactory<Program>`, override `ConfigureWebHost` to replace the `AppDbContext` registration so it uses a **single, externally-opened `NpgsqlConnection`** (one open connection shared by the test and every scoped `DbContext` the API resolves). Disable pooling on that connection string.
   - In the test base class: open one `NpgsqlConnection`, `BeginTransaction()` before each test, inject that connection into the factory, and **roll back** in `DisposeAsync()`. Because the API uses the same physical connection, its writes live inside the test's transaction and vanish on rollback.
   - **Parallelism:** each xUnit test *collection* gets its own connection + factory; xUnit parallelizes across collections. Within a collection tests run serially (they share one connection — a single connection can't multiplex). Group endpoints into a handful of collections to balance speed vs. isolation.
   - ⚠️ If the shared-connection approach proves brittle (e.g., async ambient-transaction edge cases), **fall back to Respawn**: skip the per-test transaction, and reset tables between tests with a `Respawner`. Keep this switch documented in the base class.
4. **Auth helper:** seed a `User` + linked `FamilyMember` directly, mint a JWT with `JwtFactory` using the test host's signing key, set `Authorization: Bearer` on the `HttpClient`.
5. Prove it: one test hitting `GET /health` (anonymous) and one authenticated `GET /users/me`.

**Acceptance:** two proof tests pass; a test that writes a row sees it within the test but the row is gone in the next test (rollback verified); tests in different collections run in parallel.

> Each sub-task below depends on **2.1** and can run in parallel. Per-task template: happy-path CRUD, permission boundary (non-member/non-admin → 403), validation rejection (→ 422 on the right property), relevant state transitions, and calculation/snapshot verification. Each is sized to one Cowork task; sub-tasks within a group (e.g. 2.2a/2.2b) are independent.

### Group A — Identity & access

## Task 2.2a — Own-family endpoints (`/families/mine`)
Rename (admin only); add member (admin only, auto-link User when email matches); update member (admin or self); remove member (admin only, cannot remove self). Soft-delete excludes member from queries; email-uniqueness reuse after soft-delete. Non-admin → 403; unknown member → 422.

## Task 2.2b — Global-admin endpoints (`/admin/families`)
Global-admin family + member CRUD (`AdminService.RequireGlobalAdminAsync`); list all families with members; create family; add/update/remove member across any family; auto-link User on matching email. Non-global-admin → 403.

## Task 2.3 — Groups endpoints (`/groups`)
Create group (caller's family becomes Admin GroupFamily); join via invite code (Member role, family-level — only caller's family joins); regenerate invite code (admin only); list shows only caller's groups; non-member cannot read detail (403); invalid invite code → 422.

### Group B — Activities & money

## Task 2.4 — Activities endpoints
Top-level create seeds participants from all active group members; sub-activity depth-1 guard (sub-of-sub rejected); close absorbs open subs (`AbsorbedByParent`); add/remove participant only when Open (else 422/403); update name/description only when Open.

## Task 2.5a — Expense create + split calculation
Create seeds `ExpenseParticipant` from current `ActivityParticipant` list, snapshots `WeightCalculator.GetWeight()` at `ExpenseDate`, runs `SplitCalculator`. **Assert per-participant shares against known weight inputs** (incl. rounding remainder to heaviest, excluded = 0). Verify the `audit_log` row is written atomically in the same `SaveChanges`.

## Task 2.5b — Expense update / delete guards
Update re-snapshots weights + recalculates shares only when `TotalAmount` or `ExpenseDate` change (assert no re-snapshot otherwise). Delete blocked on Settled activity and on Locked expense (→ 422/403). Audit rows for Updated/Deleted.

## Task 2.6a — Balances & settlement generation
`GET /balances` net per family (creditor positive / debtor negative); `Generate` runs `BalanceCalculator` + `SettlementOptimiser`, persists `Settlement` rows, is **idempotent** (re-call returns existing), and marks Activity Settled immediately when all balances are zero. Assert transfer count ≤ N-1.

## Task 2.6b — Settlement approval flow
`ConfirmSent` (payer-family member only, Proposed→PayerSent, creates `ApprovalStep(PayerSent)`); `ConfirmReceived` (receiver-family member only, PayerSent→Completed, `ApprovalStep(ReceiverConfirmed)`); last settlement Completed ⇒ Activity → Settled; wrong-caller → 403; `ApprovalStep` history correct. Audit rows for ConfirmSent/ConfirmReceived.

### Group C — Auth

## Task 2.6c — Auth & refresh-token flow (`/auth`)
Rotation marks old token `revoked_at` + `replaced_by_token_id` → new row; replay of an already-revoked token triggers `RevokeAllForUserAsync` (all sessions killed); logout revokes the presented row + clears cookie; login with email having no `FamilyMember` → `/not-registered`. Only the SHA-256 hash is persisted (assert plaintext never stored).

**Acceptance (2.2–2.6c):** each sub-task's happy path, ≥1 permission-boundary 403, ≥1 validation 422, and listed state transitions are covered and green against real Postgres.

---

# Phase 3 — Client unit/component tests (bUnit) + component extraction

## Task 3.1 — Scaffold `FamilySplit.Client.UnitTests` harness
**Goal:** bUnit + MudBlazor + Fluxor test infrastructure; existing store tests rehomed.
**Depends on:** 1.1 (which creates/relocates the project), 0.1.
**Steps:**
1. Add bUnit; create a `TestContext` base that registers MudBlazor services and a test `I18nText`/`AppText` so components rendering text don't fail.
2. Provide a Fluxor store/dispatcher test double so components can be rendered with arbitrary `[FeatureState]` slices.
3. Verify the moved store reducer/effect tests still pass; add `FormatHelper` tests (amount formatting per currency, deterministic `AvatarColor`).

**Acceptance:** harness renders a MudBlazor component with i18n text without errors; moved tests green; FormatHelper covered.

## Task 3.2 — Extract reusable components from pages
**Goal:** pull repeated/logic-heavy markup out of pages into `Components/Shared` so it's testable in isolation and the UI is consistent (addresses the TODOS "extract and reuse components" item).
**Depends on:** 3.1.
**Steps:**
1. Identify duplicated patterns across `Pages/**` (member tables, family/member cards, expense rows, settlement/transfer rows, balance display, stat chip rows, dialog form scaffolds).
2. Extract each into a shared component using `[Parameter, EditorRequired]` for required props; inject `I18nText` if it renders text; meet the 48dp touch-target rules.
3. Replace inline markup in pages with the new components; keep behaviour identical.
4. Register each new component in CLAUDE.md's shared-components table and `.github/copilot-instructions.md` (per the existing rule).

**Acceptance:** target pages use the extracted components; app builds and renders unchanged; new components documented.

> 3.2 is split per area below. Each sub-task: identify duplication in that area's pages, extract components into `Components/Shared`, replace inline markup, register in CLAUDE.md + `.github/copilot-instructions.md`. All depend on 3.1 and are independent of each other.

## Task 3.2a — Family area components
Pages: `Family/ManageFamily.razor`, `Family/FamilyMemberDialog.razor`, `Family/RenameFamilyDialog.razor`, `Admin/AdminFamilyDetail.razor`, `Admin/AdminFamilyList.razor`, `Admin/CreateFamilyDialog.razor`. Candidates: member table/row, family card, add/edit member dialog form scaffold, admin-vs-self guard display.

## Task 3.2b — Groups area components
Pages: `Groups/GroupList.razor`, `Groups/GroupDetail.razor`, and the group dialogs (Create/Edit/Join/AddGroupMember/AdminAddFamily). Candidates: group card, family-with-nested-members block, invite-code display/regenerate control, group stats row.

## Task 3.2c — Activities & Expenses area components
Pages: `Activities/ActivityDetail.razor` and its dialogs (Create/Edit activity, AddParticipant, Expense, QuickExpense, ExpenseDetail). Candidates: participant chip/list, sub-activity row, expense row, per-participant breakdown table, expense dialog form scaffold.

## Task 3.2d — Settlements area components
Within `Activities/ActivityDetail.razor` + `SettlementDetailDialog.razor`. Candidates: balance display (per-family +/-), transfer/settlement row, status chip mapping, Mark-sent / Mark-received action buttons + confirm dialogs (canonical labels per CLAUDE.md).

## Task 3.3 — bUnit component & page tests
**Goal:** cover UI-specific logic.
**Depends on:** 3.2.
**Steps:** test shared components' rendered output per props (StatCard, EmptyState, SectionHeader, MemberRoleChip, MemberStatusChip, GroupStatsChips, and the newly-extracted ones); test pages with mocked store state for **permission guards** (admin-only controls hidden for non-admins; own remove-button always hidden), **button disabled states** (`Disabled="@(!_isValid)"`), and conditional rendering (empty states, loading).
**Acceptance:** each shared component has a render test; permission-guard and form-validity logic asserted via bUnit; green.

---

# Phase 4 — E2E tests (Playwright + Testcontainers)

## Task 4.1 — Scaffold E2E harness
**Goal:** real browser against a fully running stack.
**Depends on:** 2.1 (reuse the container fixture + migration + JWT-seed helpers).
**Steps:**
1. Start a `PostgreSqlContainer`, apply migrations.
2. Host the API (in-process `WebApplicationFactory` or `dotnet run`) pointed at the container; serve the published Blazor WASM client.
3. Install Playwright browsers (`playwright install chromium`); launch Chromium.
4. Auth: seed the DB and inject the JWT the way the client expects (the client stores the JWT after the `/auth/return` handoff — seed a refresh cookie or stub the handoff so tests start authenticated without real Google OAuth).
5. Prove it: `login/seeded session → redirected to home`; unauthenticated → redirect to login.

**Acceptance:** Chromium drives the running app; authenticated and unauthenticated redirects verified; no `Thread.Sleep` (use `Expect(...).ToBeVisibleAsync()`).

> 4.2 is one Cowork task per flow. All depend on **4.1** and are independent. Each: role/label selectors, built-in waiting (`Expect(...).ToBeVisibleAsync()`), no `Thread.Sleep`; seed only the data that flow needs; clean up after.

## Task 4.2a — Group create + join via invite code
First family creates a group; capture the invite code; second seeded family joins via the code and both families appear in the group detail.

## Task 4.2b — Activity + expense breakdown
Create an activity, add an expense, open it, and verify the per-participant share breakdown matches the expected weight-based split.

## Task 4.2c — Settlement lifecycle
Close activity → generate settlements → **Mark sent** (payer) → **Mark received** (receiver) → activity shows **Settled**; assert status chips at each step.

## Task 4.2d — Family admin add/remove member
Admin adds a member → appears in the list; removes a member → gone. Run as a family admin.

## Task 4.2e — Permission guards
As a non-admin user: rename/remove controls are not rendered; confirm an unauthenticated visit redirects to login.

**Acceptance (4.2a–e):** each flow has a passing Playwright test; traces/screenshots captured on failure.

---

# Phase 5 — CI/CD wiring

## Task 5.1 — Wire all suites into `ci.yml`
**Goal:** every suite runs in CI behind the `ci-gate`.
**Depends on:** at least one task in each of Phases 1–4 producing runnable tests (ideally all).
**Steps:**
1. **Integration job:** remove the Neon branch + the `ConnectionStrings__DefaultConnection` env (wrong key) and the `if: false`. Run on a runner with Docker; Testcontainers manages Postgres itself, so no external secret. Filter `--filter Category=Integration`. Migrations are applied by the harness, not a CI step.
2. **Client unit job:** `dotnet test tests/FamilySplit.Client.UnitTests` with coverage.
3. **E2E job:** install Playwright browsers (`pwsh bin/.../playwright.ps1 install --with-deps chromium` or the dotnet tool), run `--filter Category=E2E`, upload traces/screenshots on failure.
4. **Server unit job:** keep as-is but ensure it now excludes client tests (project was trimmed in 1.1).
5. Update `ci-gate.needs` to include `integration-tests`, `client-unit-tests`, and `e2e-tests`; keep the skipped-or-success gate logic.
6. Tag tests with `[Trait("Category", ...)]` so filters work.

**Acceptance:** a push runs build → (server-unit, client-unit, integration, E2E, dependency-audit, secret-scan, format-check) → `ci-gate` green; integration uses Testcontainers with no external DB secret; the wrong connection-string key is gone.

---

## Suggested execution order for Cowork tasks

1. **0.1** (foundations) — must be first.
2. In parallel: **1.1**, then the **2.1** harness, and **3.1**.
3. Fan out: **1.2–1.4**; **2.2a–2.6c** (8 sub-tasks); **3.2a–3.2d → 3.3**; and (after 2.1) **4.1 → 4.2a–4.2e**.
4. **5.1** last, once each phase has runnable tests.

**Cowork task count after split:** Phase 0 = 1 · Phase 1 = 4 · Phase 2 = 9 (2.1 + eight endpoint sub-tasks) · Phase 3 = 6 (3.1, 3.2a–d, 3.3) · Phase 4 = 6 (4.1, 4.2a–e) · Phase 5 = 1 — **27 tasks total.**

## Definition of done (whole effort)
- Four green test projects covering: pure logic, client components, full backend against real Postgres, and critical UI flows.
- All suites run in CI and gate merges.
- CLAUDE.md and `.github/copilot-instructions.md` reflect the real stack (Moq, 4 projects, Testcontainers isolation, extracted shared components).
- Coverage report published in CI; calculators/validators near 100%, endpoints exercised end-to-end.

---

## Completion notes

**All 27 tasks completed.** Key implementation decisions recorded here for future reference.

### What was built

| Project | Contents |
|---|---|
| `FamilySplit.UnitTests` | Pure logic: `WeightCalculator`, `SplitCalculator`, `BalanceCalculator`, `SettlementOptimiser`, `ParticipantSeeder`, all FluentValidation validators, extracted business guards (`SettlementStateMachine`, `ExpenseReshuffleRequired`), service/middleware/endpoint smoke tests |
| `FamilySplit.Client.UnitTests` | Fluxor reducer/effect tests (moved from UnitTests), Refit client tests, bUnit component tests for all shared components including `StatCard`, `EmptyState`, `SectionHeader`, `MemberRoleChip`, `MemberStatusChip`, `SettlementRow`, `MemberActionCell`, plus permission-guard page tests for `ManageFamily` |
| `FamilySplit.IntegrationTests` | Full HTTP→service→PostgreSQL tests via Testcontainers + transaction-rollback isolation. Covers: own-family, global-admin, groups, activities, expenses (create/update/delete), settlements (balances, generate, confirm-sent, confirm-received), auth (refresh rotation, theft detection, logout) |
| `FamilySplit.E2ETests` | Playwright + Testcontainers + subprocess API + in-process static client server. Flows: group create/join, activity+expense, settlement lifecycle, family admin, permission guards |

### Shared components extracted

| Component | Extracted from |
|---|---|
| `SettlementRow` | `ActivityDetail.razor` and `GroupDetail.razor` (near-identical 55-line loops) |
| `MemberActionCell` | `ManageFamily.razor` and `AdminFamilyDetail.razor` (edit/remove icon-button pair) |

### 3.2b and 3.2c assessment

Tasks 3.2b (groups area) and 3.2c (activities/expenses area) were audited and found to have **no qualifying extraction candidates**:
- The family-with-member-chips pattern in `GroupDetail` vs `ActivityDetail` uses different DTOs (`GroupMemberDto` vs `ActivityParticipantDto`) and different interaction modes (linked-status chips vs closeable chips) — extraction would be artificially generic.
- The per-participant breakdown table exists only in `ExpenseDetailDialog` (one place).
- Sub-activity rows exist only in `ActivityDetail` (one place).

Per CLAUDE.md rule: "Do not create a new shared component for logic that appears in only one place."

### CI coverage

Both the server-unit and client-unit CI jobs publish Cobertura coverage reports to the job summary and as downloadable artifacts. Integration and E2E tests run against real Postgres via Testcontainers (no external DB secret required).
