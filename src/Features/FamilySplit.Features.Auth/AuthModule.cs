using System.Text;
using System.Threading.RateLimiting;
using FamilySplit.Common.Modules;
using FamilySplit.Common.Routing;
using FamilySplit.Features.Auth.Data;
using FamilySplit.Features.Auth.Endpoints;
using FamilySplit.Features.Auth.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FamilySplit.Features.Auth;

/// <summary>
/// The Auth feature slice: OAuth/PKCE login, refresh-token rotation, logout.
/// Unlike every other slice, Auth is a token-exchange protocol rather than domain
/// CQRS — <c>Login</c>/<c>Callback</c>/<c>Refresh</c>/<c>Logout</c> stay as endpoint
/// lambdas (cookie/redirect orchestration is HTTP-edge logic), and there is no
/// <c>I{Slice}Data</c> seam. <see cref="Data.OAuthData"/> and
/// <see cref="Data.RefreshTokenData"/> are the slice's data-access gateways.
///
/// <para>
/// <see cref="RegisterServices"/> also takes over what used to be inline
/// Program.cs wiring: the Jwt config/signing-key validation, JwtBearer
/// authentication (incl. the SignalR <c>?access_token=</c> plumbing), the
/// "google-oauth" HttpClient + resilience pipeline, and the "auth" named
/// rate-limit policy (via a second <c>AddRateLimiter</c> call — ASP.NET Core
/// composes every registered options delegate, so this does not clobber the
/// host's own <c>AddRateLimiter</c> call for the global limiter + OnRejected).
/// </para>
/// </summary>
public sealed class AuthModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // --- JwtBearer authentication --------------------------------------------
        var jwt = configuration.GetSection("Jwt");
        var signingKey = jwt["SigningKey"]
            ?? throw new InvalidOperationException("Missing config Jwt:SigningKey. Set via user-secrets or env.");
        if (Encoding.UTF8.GetByteCount(signingKey) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (256 bits) for HMAC-SHA256.");
        var issuer = jwt["Issuer"] ?? "familysplit";
        var audience = jwt["Audience"] ?? "familysplit-client";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured via IConfigureOptions<JwtBearerOptions> (rather than the
        // AddJwtBearer(options => ...) delegate directly) so RequireHttpsMetadata
        // can depend on IWebHostEnvironment without RegisterServices needing the
        // environment at module-registration time — IFeatureModule only receives
        // IServiceCollection + IConfiguration.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IWebHostEnvironment>((options, env) =>
            {
                // Use the modern JsonWebTokenHandler — faster than the legacy
                // JwtSecurityTokenHandler and the future-default for JwtBearer.
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.RequireHttpsMetadata = !env.IsDevelopment();

                // SignalR: Blazor WASM cannot set Authorization headers on WebSocket
                // upgrade requests. The client passes the JWT as ?access_token= instead.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(HubPaths.Prefix))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    // sub claim from JwtFactory holds the User.Id Guid.
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        // --- "auth" named rate-limit policy ---------------------------------------
        // A second AddRateLimiter call composes with the host's own call (which
        // keeps the GlobalLimiter + RejectionStatusCode + OnRejected) — ASP.NET
        // Core's RateLimiterOptions configuration is additive across every
        // registered IConfigureOptions<RateLimiterOptions>, so AddPolicy here does
        // not clobber the host's global limiter registration.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(RateLimitPolicies.Auth, ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,        // 15-second resolution
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    }));
        });

        // --- "google-oauth" HttpClient + resilience --------------------------------
        // Timeout is delegated entirely to the resilience pipeline below, so we use
        // InfiniteTimeSpan here — the TotalRequestTimeout handler acts as the backstop.
        // Two retries handle transient Google 5xx/network blips without hammering the
        // endpoint; exponential backoff with jitter avoids synchronized retry storms.
        services.AddHttpClient("google-oauth", c =>
        {
            c.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        })
        .AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 2;
            o.Retry.UseJitter = true;
            o.Retry.Delay = TimeSpan.FromMilliseconds(300);
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(12);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(35);
        });

        // --- Slice services ---------------------------------------------------------
        // JwtFactory issues JWTs after OAuth callback. OAuthData exchanges codes
        // against Google and upserts the User row. RefreshTokenData issues/rotates/
        // revokes refresh_tokens rows.
        services.AddSingleton<JwtFactory>();
        services.AddSingleton<PkceFlow>();
        services.AddScoped<OAuthData>();
        services.AddScoped<RefreshTokenData>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
    }
}
