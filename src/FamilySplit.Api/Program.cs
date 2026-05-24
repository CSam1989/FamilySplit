using System.Security.Claims;
using System.Text;
using FamilySplit.Api.Auth;
using FamilySplit.Api.Endpoints;
using FamilySplit.Api.Middleware;
using FamilySplit.Application;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging -----------------------------------------------------------------------
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- CORS for Blazor WASM client --------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "https://localhost:5001", "http://localhost:5000" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// --- HTTP client factory (needed by OAuthHandler for token exchange) -------------
builder.Services.AddHttpClient();

// --- Application + Infrastructure -------------------------------------------------
builder.Services.AddFamilySplitApplication();
builder.Services.AddFamilySplitInfrastructure(builder.Configuration);

// --- Auth: JwtBearer + OAuth handler placeholders ---------------------------------
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"]
    ?? throw new InvalidOperationException("Missing config Jwt:SigningKey. Set via user-secrets or env.");
var issuer   = jwt["Issuer"]   ?? "familysplit";
var audience = jwt["Audience"] ?? "familysplit-client";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// JwtFactory issues JWTs after OAuth callback. OAuthHandler exchanges codes
// against Google and upserts the User row.
builder.Services.AddSingleton<JwtFactory>();
builder.Services.AddSingleton<PkceFlow>();
builder.Services.AddScoped<OAuthHandler>();

// Persist Data Protection keys to a stable folder so OAuth state cookies and
// any other protected payloads survive API restarts in dev.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, ".dp-keys")))
    .SetApplicationName("FamilySplit");

// --- OpenAPI / Scalar -------------------------------------------------------------
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Middleware pipeline ----------------------------------------------------------
app.UseSerilogRequestLogging();
app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseCors("ClientApp");
app.UseAuthentication();
app.UseAuthorization();

// --- Health (anonymous) -----------------------------------------------------------
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "FamilySplit.Api",
    utc = DateTimeOffset.UtcNow
})).AllowAnonymous();

// --- Endpoint groups --------------------------------------------------------------
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapFamilyMemberEndpoints();   // GET /users/me/profile
app.MapAdminEndpoints();          // /admin/families — global-admin CRUD
app.MapFamilyEndpoints();         // /families/mine — own-family management
app.MapGroupEndpoints();          // /groups — CRUD + join + invite-code
app.MapGroupMemberEndpoints();    // no-op stub (members managed via Family endpoints)
app.MapActivityEndpoints();       // /groups/{groupId}/activities — CRUD + participants + close
app.MapExpenseEndpoints();        // /groups/{groupId}/activities/{activityId}/expenses — Phase 5
app.MapSettlementEndpoints();     // /groups/{groupId}/activities/{activityId}/settlements — Phase 6


// --- OpenAPI + Scalar UI (dev only) ----------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Run();

// Small helper used by future endpoints to pull the caller's UserId from the JWT.
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("sub")
                  ?? throw new UnauthorizedAccessException("JWT missing sub/NameIdentifier claim.");
        return Guid.Parse(sub);
    }
}
