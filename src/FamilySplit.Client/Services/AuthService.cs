using System.Net;
using Microsoft.Extensions.Logging;
using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// In-memory holder for the short-lived JWT.
///
/// Persistence is handled by the API's HttpOnly refresh cookie — neither the
/// JWT nor the refresh secret ever touch <c>localStorage</c> or
/// <c>sessionStorage</c>, so an XSS-injected script cannot exfiltrate them.
///
/// Lifecycle:
///   • App boot → <see cref="TryRefreshAsync"/> attempts a silent refresh.
///     Success leaves a JWT in memory; failure (no cookie) means the user
///     needs to sign in.
///   • Each API call → <see cref="GetTokenAsync"/> returns the cached JWT.
///     If it is missing or within the pre-expiry window it triggers a refresh.
///   • 401 from an API call → <see cref="JwtAuthHandler"/> calls
///     <see cref="TryRefreshAsync"/> once and retries the request.
///   • Sign-out → <see cref="LogoutAsync"/> revokes the refresh row server-side
///     and clears the in-memory JWT.
/// </summary>
public class AuthService
{
    /// <summary>Refresh proactively when the JWT has less than this much life left.</summary>
    private static readonly TimeSpan PreemptiveWindow = TimeSpan.FromSeconds(30);

    private readonly IAuthApi _authApi;
    private readonly ILogger<AuthService> _logger;

    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    // Single-flight: collapse concurrent refresh attempts onto one network call.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthService(IAuthApi authApi, ILogger<AuthService> logger)
    {
        _authApi = authApi;
        _logger = logger;
    }

    public bool HasValidToken =>
        _token is not null && DateTimeOffset.UtcNow + PreemptiveWindow < _expiresAt;

    /// <summary>
    /// Returns a valid JWT or null. Triggers a silent refresh when the cached
    /// token is missing or within the pre-expiry window.
    /// </summary>
    public async ValueTask<string?> GetTokenAsync()
    {
        if (HasValidToken) return _token;
        await TryRefreshAsync();
        return HasValidToken ? _token : null;
    }

    /// <summary>True if the in-memory token is present and not yet near expiry.</summary>
    public ValueTask<bool> IsAuthenticatedAsync() =>
        HasValidToken ? ValueTask.FromResult(true) : SlowPath();

    private async ValueTask<bool> SlowPath()
    {
        await TryRefreshAsync();
        return HasValidToken;
    }

    /// <summary>
    /// Attempts a silent refresh. Returns true on success, false when no valid
    /// refresh cookie exists (the caller should show the sign-in screen).
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        // Coalesce concurrent calls.
        await _refreshLock.WaitAsync();
        try
        {
            if (HasValidToken) return true;

            var response = await _authApi.RefreshAsync();
            if (string.IsNullOrWhiteSpace(response.Token)) return false;

            _token = response.Token;
            _expiresAt = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(response.ExpiresInSeconds);
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // No refresh cookie or it has expired / been revoked. Expected on first visit.
            _token = null;
            _expiresAt = DateTimeOffset.MinValue;
            return false;
        }
        catch (Exception ex)
        {
            // Network errors etc. Don't drop the cached token in case it is still good.
            _logger.LogWarning(ex, "Silent refresh failed");
            return HasValidToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Drops the in-memory token. Used after a 401 chain that can't recover.</summary>
    public void ClearTokenInMemory()
    {
        _token = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    /// <summary>Revokes the refresh token server-side and clears the in-memory JWT.</summary>
    public async Task LogoutAsync()
    {
        try { await _authApi.LogoutAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Logout call failed; clearing local state anyway."); }
        ClearTokenInMemory();
    }
}
