using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Api.Auth;

/// <summary>
/// Exchanges a Google OAuth authorization code (with PKCE verifier) for an access token,
/// fetches the user profile from Google's OIDC userinfo endpoint, then:
/// <list type="number">
///   <item>Upserts a <see cref="User"/> row by (provider, external_id).</item>
///   <item>Finds the <see cref="FamilyMember"/> whose email matches the login email.</item>
///   <item>Links them (sets FamilyMember.UserId) on first login.</item>
///   <item>Throws <see cref="NotRegisteredException"/> if no matching FamilyMember exists.</item>
/// </list>
/// </summary>
public class OAuthHandler
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthHandler> _logger;

    public OAuthHandler(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthHandler> logger)
    {
        _db = db;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<User> HandleGoogleCallbackAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken ct)
    {
        var section = _config.GetSection("OAuth:Google");
        var clientId = section["ClientId"]
            ?? throw new InvalidOperationException("Missing OAuth:Google:ClientId user-secret.");
        var clientSecret = section["ClientSecret"]
            ?? throw new InvalidOperationException("Missing OAuth:Google:ClientSecret user-secret.");
        var tokenUrl = section["TokenUrl"] ?? "https://oauth2.googleapis.com/token";
        var userInfoUrl = section["UserInfoUrl"] ?? "https://openidconnect.googleapis.com/v1/userinfo";

        var http = _httpClientFactory.CreateClient("google-oauth");

        // 1. Exchange authorization code + PKCE verifier for tokens.
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier
            })
        };

        using var tokenResponse = await http.SendAsync(tokenRequest, ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var body = await tokenResponse.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Google token exchange failed: {Status} {Body}", tokenResponse.StatusCode, body);
            throw new InvalidOperationException("Google token exchange failed.");
        }

        var tokens = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct)
            ?? throw new InvalidOperationException("Google returned empty token response.");

        // 2. Fetch user profile.
        var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        using var userInfoResponse = await http.SendAsync(userInfoRequest, ct);
        userInfoResponse.EnsureSuccessStatusCode();

        var profile = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(ct)
            ?? throw new InvalidOperationException("Google returned empty userinfo response.");

        if (string.IsNullOrWhiteSpace(profile.Sub) || string.IsNullOrWhiteSpace(profile.Email))
            throw new InvalidOperationException("Google userinfo missing sub or email.");

        // Normalize the email up-front so we both store the canonical form on
        // the User row and use the same form for the FamilyMember lookup. This
        // lets Postgres use the unique index on family_members.email (which
        // would otherwise be defeated by a runtime LOWER() call).
        var emailLower = profile.Email.Trim().ToLowerInvariant();

        // 3. Upsert User by (Provider, ExternalId).
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Provider == Provider.Google && u.ExternalId == profile.Sub, ct);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Provider = Provider.Google,
                ExternalId = profile.Sub,
                Email = emailLower,
                DisplayName = profile.Name ?? profile.Email,
                AvatarUrl = profile.Picture,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
            _logger.LogInformation("Created user {UserId} for Google sub {Sub}", user.Id, profile.Sub);
        }
        else
        {
            user.Email = emailLower;
            user.DisplayName = profile.Name ?? profile.Email;
            user.AvatarUrl = profile.Picture;
        }

        // 4. Find the FamilyMember whose email matches this login email.
        //    Both columns now store lowercase, so a direct equality predicate
        //    uses the existing unique index.
        var member = await _db.FamilyMembers
            .FirstOrDefaultAsync(m => m.Email == emailLower, ct);

        if (member is null)
        {
            // No family member registered for this email — block login.
            _logger.LogWarning("Login rejected: no FamilyMember registered for email {Email}", profile.Email);
            throw new NotRegisteredException(profile.Email);
        }

        // 5. Link on first login. Guard against a different user stealing the slot.
        if (member.UserId is null)
        {
            member.UserId = user.Id;
            _logger.LogInformation(
                "Linked FamilyMember {MemberId} to User {UserId}", member.Id, user.Id);
        }
        else if (member.UserId != user.Id)
        {
            _logger.LogWarning(
                "FamilyMember {MemberId} already linked to a different User — rejecting login for {UserId}",
                member.Id, user.Id);
            throw new NotRegisteredException(profile.Email);
        }

        await _db.SaveChangesAsync(ct);
        return user;
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record GoogleUserInfo(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("picture")] string? Picture);
}

/// <summary>
/// Thrown when a user's OAuth email does not match any registered FamilyMember.
/// The auth callback redirects to /not-registered instead of issuing a JWT.
/// </summary>
public class NotRegisteredException(string email) : Exception($"No family member is registered for {email}.");
