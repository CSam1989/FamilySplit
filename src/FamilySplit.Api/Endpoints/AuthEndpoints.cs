using FamilySplit.Api.Auth;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;

namespace FamilySplit.Api.Endpoints;

public static class AuthEndpoints
{
    private const string StateCookie   = "fs_oauth_state";
    private const string HandoffCookie = "fs_handoff";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").AllowAnonymous();

        // -----------------------------------------------------------------------------
        // GET /auth/login/Google
        //
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

            var clientId = config["OAuth:Google:ClientId"]
                ?? throw new InvalidOperationException("Missing OAuth:Google:ClientId user-secret.");
            var authorizeUrl = config["OAuth:Google:AuthorizeUrl"] ?? "https://accounts.google.com/o/oauth2/v2/auth";

            var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Absolute)
                ? returnUrl!
                : config["Cors:AllowedOrigins:0"] ?? "https://localhost:5001";

            var flow = pkce.NewFlow(safeReturnUrl);
            var codeChallenge = pkce.DeriveCodeChallenge(flow.CodeVerifier);
            var protectedPayload = pkce.Protect(flow);

            http.Response.Cookies.Append(StateCookie, protectedPayload, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax, // required: callback is a top-level cross-site GET
                Path = "/auth",
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true
            });

            // Mirror the path used by the callback endpoint so it matches Google's registered redirect URI exactly.
            var redirectUri = BuildRedirectUri(http, "Google");

            var query = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"]             = clientId,
                ["redirect_uri"]          = redirectUri,
                ["response_type"]         = "code",
                ["scope"]                 = "openid email profile",
                ["state"]                 = flow.State,
                ["code_challenge"]        = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["access_type"]           = "online",
                ["prompt"]                = "select_account"
            });

            return Results.Redirect(authorizeUrl + query.ToUriComponent());
        });

        // -----------------------------------------------------------------------------
        // GET /auth/callback/Google?code=...&state=...
        //
        // Validates state, exchanges code for tokens, fetches userinfo, upserts the User,
        // mints a JWT, drops it in a short-lived HttpOnly handoff cookie, and redirects
        // back to the Blazor return page.
        // -----------------------------------------------------------------------------
        group.MapGet("/callback/{provider}", async (
            string provider,
            string? code,
            string? state,
            string? error,
            HttpContext http,
            PkceFlow pkce,
            OAuthHandler handler,
            JwtFactory jwtFactory,
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
                // No FamilyMember registered for this email — redirect to the "not registered" page.
                http.Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/auth" });
                return Results.Redirect(flow.ReturnUrl.TrimEnd('/') + "/not-registered");
            }

            var jwt = jwtFactory.Create(user);

            http.Response.Cookies.Append(HandoffCookie, jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/auth/handoff",
                MaxAge = TimeSpan.FromSeconds(60),
                IsEssential = true
            });

            return Results.Redirect(flow.ReturnUrl.TrimEnd('/') + "/auth/return");
        });

        // -----------------------------------------------------------------------------
        // GET /auth/handoff
        //
        // One-shot exchange: read the handoff cookie, return the JWT in the JSON body,
        // and clear the cookie. Called by the Blazor /auth/return page with credentials.
        // -----------------------------------------------------------------------------
        group.MapGet("/handoff", (HttpContext http) =>
        {
            var jwt = http.Request.Cookies[HandoffCookie];
            http.Response.Cookies.Delete(HandoffCookie, new CookieOptions { Path = "/auth/handoff" });

            if (string.IsNullOrWhiteSpace(jwt))
                return Results.Unauthorized();

            return Results.Ok(new { token = jwt });
        });

        // -----------------------------------------------------------------------------
        // POST /auth/refresh — placeholder. MSAL silent refresh + refresh-token store
        // lands later. Returning 501 until then.
        // -----------------------------------------------------------------------------
        group.MapPost("/refresh", () => Results.StatusCode(StatusCodes.Status501NotImplemented));

        return app;
    }

    private static string BuildRedirectUri(HttpContext http, string providerName)
        => $"{http.Request.Scheme}://{http.Request.Host}/auth/callback/{providerName}";

    private static string GetReturnUrlFromState(PkceFlow pkce, HttpContext http)
    {
        var flow = pkce.Unprotect(http.Request.Cookies[StateCookie]);
        return flow?.ReturnUrl ?? "/";
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
