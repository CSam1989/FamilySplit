using FamilySplit.Api.Auth;
using FamilySplit.Application.Auth;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;

namespace FamilySplit.Api.Endpoints;

public static class AuthEndpoints
{
    private const string StateCookie = "fs_oauth_state";
    private const string RefreshCookie = "fs_refresh";

    /// <summary>
    /// The refresh cookie is scoped to the auth subtree so it is only sent on
    /// /auth/refresh and /auth/logout — nothing else can read or forward it.
    /// </summary>
    private const string RefreshCookiePath = "/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").AllowAnonymous().RequireRateLimiting("auth");

        // -----------------------------------------------------------------------------
        // GET /auth/login/Google
        // Generates state + PKCE verifier, stores them encrypted in an HttpOnly cookie,
        // and 302-redirects the browser to Google's consent screen.
        // -----------------------------------------------------------------------------
        group.MapGet("/login/{provider}", (
            string provider,
            string? returnUrl,
            HttpContext http,
            IConfiguration config,
            PkceFlow pkce) =>
        {
            if (!Enum.TryParse<Provider>(provider, ignoreCase: true, out var p) || p != Provider.Google)
                return Results.BadRequest(new { error = "Unsupported provider", provider });

            var clientId = config["OAuth:Google:ClientId"] ?? throw new InvalidOperationException("Missing OAuth:Google:ClientId user-secret.");
            var authorizeUrl = config["OAuth:Google:AuthorizeUrl"] ?? "https://accounts.google.com/o/oauth2/v2/auth";

            // Open-redirect guard.
            var allowed = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "https://localhost:5001" };
            var safeReturnUrl = IsAllowedReturnUrl(returnUrl, allowed)
                ? returnUrl!
                : allowed[0];

            var flow = pkce.NewFlow(safeReturnUrl);
            var codeChallenge = pkce.DeriveCodeChallenge(flow.CodeVerifier);
            var protectedPayload = pkce.Protect(flow);

            http.Response.Cookies.Append(StateCookie, protectedPayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax, // callback is a top-level cross-site GET from Google
                Path = "/auth",
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true,
            });

            var redirectUri = BuildRedirectUri(http, "Google");

            var query = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = flow.State,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["access_type"] = "online",
                ["prompt"] = "select_account",
            });

            return Results.Redirect(authorizeUrl + query.ToUriComponent());
        });

        // -----------------------------------------------------------------------------
        // GET /auth/callback/Google?code=...&state=...
        // Validates state, exchanges code for tokens, fetches userinfo, upserts the User,
        // ISSUES A REFRESH TOKEN into an HttpOnly cookie, and redirects to /auth/return.
        // The browser immediately calls /auth/refresh from that page to obtain the JWT.
        // -----------------------------------------------------------------------------
        group.MapGet("/callback/{provider}", async (
            string provider,
            string? code,
            string? state,
            string? error,
            HttpContext http,
            PkceFlow pkce,
            OAuthHandler handler,
            RefreshTokenService refreshTokens,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("AuthCallback");

            if (!Enum.TryParse<Provider>(provider, ignoreCase: true, out var p) || p != Provider.Google)
                return Results.BadRequest(new { error = "Unsupported provider", provider });

            if (!string.IsNullOrWhiteSpace(error))
            {
                logger.LogInformation("Google returned OAuth error: {Error}", error);
                return Results.Redirect(GetReturnUrlFromState(pkce, http) + "?error=" + Uri.EscapeDataString(error));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return Results.BadRequest(new { error = "Missing code or state" });

            var flow = pkce.Unprotect(http.Request.Cookies[StateCookie]);
            http.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/auth" });

            if (flow is null)
                return Results.BadRequest(new { error = "Missing or invalid state cookie. Restart the login flow." });

            if (!CryptographicEquals(flow.State, state))
            {
                logger.LogWarning("OAuth state mismatch — possible CSRF attempt.");
                return Results.BadRequest(new { error = "State mismatch" });
            }

            var redirectUri = BuildRedirectUri(http, "Google");

            User user;
            try
            {
                user = await handler.HandleGoogleCallbackAsync(code, flow.CodeVerifier, redirectUri, ct);
            }
            catch (NotRegisteredException)
            {
                return Results.Redirect(flow.ReturnUrl.TrimEnd('/') + "/not-registered");
            }

            var issued = await refreshTokens.IssueAsync(
                user.Id,
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString(),
                ct);

            WriteRefreshCookie(http, issued.Secret, issued.ExpiresAt);

            return Results.Redirect(flow.ReturnUrl.TrimEnd('/') + "/auth/return");
        });

        // -----------------------------------------------------------------------------
        // POST /auth/refresh
        // Reads the refresh cookie, rotates it, and returns a fresh JWT.
        // Called by AuthService:
        //   • immediately after /auth/return (post-OAuth)
        //   • on app boot before the user can interact (silent refresh)
        //   • when the in-memory JWT is about to expire
        //   • when an API call returned 401
        // -----------------------------------------------------------------------------
        group.MapPost("/refresh", async (
            HttpContext http,
            RefreshTokenService refreshTokens,
            JwtFactory jwtFactory,
            FamilySplit.Infrastructure.AppDbContext db,
            CancellationToken ct) =>
        {
            var presented = http.Request.Cookies[RefreshCookie];

            // CancellationToken.None is intentional for all DB work past this point.
            // Once RotateAsync commits, the old token is revoked and the new one exists only in
            // the DB. If the client disconnects before we write the cookie/JWT, the browser's
            // next refresh attempt presents the now-revoked token, which triggers theft-detection
            // and kills every session for that user. Using ct here would create that window.
            var rotateResult = await refreshTokens.RotateAsync(
                presented ?? "",
                http.Connection.RemoteIpAddress?.ToString(),
                http.Request.Headers.UserAgent.ToString(),
                CancellationToken.None);

            // Accept both a full rotation (new cookie) and a within-window reuse (existing cookie kept).
            // ConcurrentRetry and Rejected both return 401; only Rejected also clears the cookie.
            if (rotateResult is not RefreshTokenService.RotateResult.Success
                            and not RefreshTokenService.RotateResult.Reused)
            {
                // ConcurrentRetry: the browser already holds the correct replacement cookie
                // from the first winning rotation — clearing it here would remove the only
                // valid cookie the client has, causing an immediate logged-out state.
                // Only clear the cookie for genuine failures (Rejected).
                if (rotateResult is RefreshTokenService.RotateResult.Rejected)
                    ClearRefreshCookie(http);

                return Results.Unauthorized();
            }

            var userId = rotateResult switch
            {
                RefreshTokenService.RotateResult.Success s => s.UserId,
                RefreshTokenService.RotateResult.Reused r => r.UserId,
                _ => throw new InvalidOperationException("Unhandled RotateResult"),
            };

            // Load the user record once so the JWT carries the right claims.
            var user = await db.Users.FindAsync(new object?[] { userId }, CancellationToken.None);
            if (user is null)
            {
                ClearRefreshCookie(http);
                return Results.Unauthorized();
            }

            // Only update the cookie when a new token was issued; for Reused the
            // browser already holds the still-active cookie — touching it is unnecessary.
            if (rotateResult is RefreshTokenService.RotateResult.Success rotated)
                WriteRefreshCookie(http, rotated.Secret, rotated.ExpiresAt);

            var jwt = jwtFactory.Create(user);
            return Results.Ok(new
            {
                token = jwt,
                expiresInSeconds = jwtFactory.LifetimeMinutes * 60,
            });
        });

        // -----------------------------------------------------------------------------
        // POST /auth/logout
        // Revokes the refresh row server-side and clears the cookie. The in-memory
        // JWT on the client is also dropped by AuthService.
        // -----------------------------------------------------------------------------
        group.MapPost("/logout", async (
            HttpContext http,
            RefreshTokenService refreshTokens,
            CancellationToken ct) =>
        {
            var presented = http.Request.Cookies[RefreshCookie];
            await refreshTokens.RevokeAsync(presented, ct);
            ClearRefreshCookie(http);
            return Results.NoContent();
        });

        return app;
    }

    // ── Cookie helpers ────────────────────────────────────────────────────────

    private static void WriteRefreshCookie(HttpContext http, string secret, DateTimeOffset expiresAt)
    {
        http.Response.Cookies.Append(RefreshCookie, secret, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expiresAt,
            IsEssential = true,
        });
    }

    private static void ClearRefreshCookie(HttpContext http)
    {
        http.Response.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            Path = RefreshCookiePath,
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
        });
    }

    private static string BuildRedirectUri(HttpContext http, string providerName)
        => $"{http.Request.Scheme}://{http.Request.Host}/auth/callback/{providerName}";

    private static string GetReturnUrlFromState(PkceFlow pkce, HttpContext http)
    {
        var flow = pkce.Unprotect(http.Request.Cookies[StateCookie]);
        return flow?.ReturnUrl ?? "/";
    }

    /// <summary>
    /// Accepts a candidate returnUrl only when it is a well-formed absolute URL
    /// whose origin (scheme + host + port) appears in the configured allow-list.
    /// </summary>
    private static bool IsAllowedReturnUrl(string? candidate, string[] allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        foreach (var origin in allowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var allowed)) continue;
            if (string.Equals(uri.Scheme, allowed.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, allowed.Host, StringComparison.OrdinalIgnoreCase)
                && uri.Port == allowed.Port)
                return true;
        }
        return false;
    }

    /// <summary>Constant-time string comparison to avoid timing leaks on state validation.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
