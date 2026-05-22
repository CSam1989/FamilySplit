# FamilySplit

Group-based expense splitting with age-weighted family shares. See `FamilySplit_Blueprint_3.md` (in the project knowledge) for the full design and `docs/copilot-instructions.md` for code conventions.

> **Status: Phase 1 — Foundation scaffold.** Solution structure, MudBlazor shell, JWT middleware, and `/health` endpoint are in place. OAuth code paths exist as stubs; EF Core migrations have not been generated yet.

## 1. Prerequisites

- .NET 10 SDK. Pinned in `global.json`.
- Docker (for the local Postgres container) — or a Neon dev branch.
- A Google Cloud project if you want to test OAuth end-to-end.

## 2. First run

```bash
# 1. Start Postgres
cp .env.example .env
docker compose up -d postgres

# 2. Restore + build
dotnet restore
dotnet build

# 3. Set required secrets (per-developer, never committed)
cd src/FamilySplit.Api
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
# Postgres connection string only needed here if you don't want to use the one in
# appsettings.Development.json — for the docker-compose default, you can skip this.

# 4. Run the API
dotnet run --project src/FamilySplit.Api

# 5. In another terminal, run the Blazor client
dotnet run --project src/FamilySplit.Client
```

Then open `https://localhost:5001`, hit **API health** in the side nav, and confirm `/health` returns `{ "status": "ok" }`.

Optional: bring up pgAdmin at <http://localhost:8081> with `docker compose --profile tools up -d pgadmin`.

## 3. Solution layout

```
FamilySplit.Domain          Entities + enums. Zero dependencies.
FamilySplit.Application     Service classes, FluentValidation validators, Core/ (calculators).
FamilySplit.Infrastructure  AppDbContext, EF Core configurations, migrations.
FamilySplit.Api             Minimal API endpoints, OAuth/JWT, middleware, Dockerfile.
FamilySplit.Client          Blazor WebAssembly + MudBlazor + Fluxor + Refit.
tests/FamilySplit.UnitTests xUnit tests for calculators.
```

## 4. OAuth setup (do this before Phase 1.2)

`OAuthHandler` throws `NotImplementedException` until you fill in real credentials. Register a Google OAuth client per environment (dev / prod) and store secrets via `dotnet user-secrets` — never commit them.

### 4.1 Google

1. Go to <https://console.cloud.google.com/apis/credentials> and create a project (e.g. `familysplit-dev`).
2. **OAuth consent screen** → External → fill app name / support email / dev contact. Add scope `openid email profile`.
3. **Credentials → Create credentials → OAuth client ID** → Application type **Web application**.
   - Authorized JavaScript origins: `https://localhost:5001`
   - Authorized redirect URIs: `https://localhost:5081/auth/callback/Google`
4. Copy the **Client ID** and **Client secret** into user secrets:

```bash
cd src/FamilySplit.Api
dotnet user-secrets set "OAuth:Google:ClientId"     "<client_id>"
dotnet user-secrets set "OAuth:Google:ClientSecret" "<client_secret>"
```

### 4.2 Required secret keys (summary)

| Key | Used by |
|---|---|
| `Jwt:SigningKey` | API JwtBearer middleware + JwtFactory (min 32 chars / 256-bit) |
| `ConnectionStrings:Postgres` | Infrastructure DI (`Host=...;Database=...;Username=...;Password=...`) |
| `OAuth:Google:ClientId` / `ClientSecret` | OAuthHandler |

## 5. Database

The repo defaults to local Postgres via docker-compose. To point at a Neon dev branch instead, override the connection string:

```bash
cd src/FamilySplit.Api
dotnet user-secrets set "ConnectionStrings:Postgres" \
  "Host=<host>.neon.tech;Database=familysplit;Username=<user>;Password=<pwd>;SSL Mode=Require;Trust Server Certificate=true"
```

Initial migration (run once, then commit the `Migrations/` folder under `src/FamilySplit.Infrastructure`):

```bash
# Install the EF Core CLI tool once per machine if you don't have it.
dotnet tool install --global dotnet-ef

# Generate the migration. Does not touch the DB — just scaffolds C# files describing the schema.
dotnet ef migrations add Initial \
  --project src/FamilySplit.Infrastructure \
  --startup-project src/FamilySplit.Api \
  --output-dir Migrations

# Apply to the running Postgres. Make sure `docker compose up -d postgres` is running first.
dotnet ef database update \
  --project src/FamilySplit.Infrastructure \
  --startup-project src/FamilySplit.Api
```

Subsequent schema changes use the same commands with a new migration name (`dotnet ef migrations add AddInviteCodeIndex`, etc.).

## 6. Phase 1 — what's done and what's next

**Done:**

- Solution + 5 projects + tests project, all referencing each other correctly.
- Domain entities & enums per blueprint §4.
- `AppDbContext` + per-entity `IEntityTypeConfiguration<T>` for every table.
- API: `Program.cs` wires Serilog, CORS, JwtBearer, Authorization, ValidationExceptionMiddleware, OpenAPI/Scalar, `/health`.
- Auth scaffolding: `JwtFactory`, `OAuthHandler` stub, `/auth/login/{provider}` + `/auth/callback/{provider}` endpoints.
- Blazor Client: MudBlazor layout, NavMenu, Dashboard, Health page calling `IHealthApi` via Refit, Fluxor wired.
- docker-compose with Postgres 17 + optional pgAdmin.
- `.gitignore`, `.editorconfig`, `global.json`, `Directory.Build.props`.

**Next (Phase 1.2 — same phase, not yet done):**

1. Register the Google OAuth app and paste credentials into user-secrets.
2. Finish `OAuthHandler.HandleCallbackAsync` — code → token exchange, userinfo fetch, `User` upsert.
3. Generate the initial EF Core migration and apply it.
4. Smoke-test the full flow: `/auth/login/Google` → redirect → `/auth/callback/Google` → JWT → `Authorization: Bearer` against a protected stub endpoint.
5. Add a JWT-attaching `DelegatingHandler` to the Blazor `HttpClient`.

## 7. Useful commands

```bash
dotnet format                          # apply style rules
dotnet test                            # run unit tests
docker compose up -d postgres          # start dev DB
docker compose --profile tools up -d   # start DB + pgAdmin
docker compose down                    # stop everything
```
