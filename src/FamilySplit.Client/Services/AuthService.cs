using Microsoft.JSInterop;

namespace FamilySplit.Client.Services;

/// <summary>
/// Thin wrapper over browser storage for the FamilySplit JWT.
///
/// Two storage modes:
///   persistent = true  → localStorage  (survives browser restarts; "remember me")
///   persistent = false → sessionStorage (tab-scoped; cleared when the tab closes)
///
/// GetTokenAsync checks localStorage first so that a remembered session is found
/// even after a browser restart, then falls back to sessionStorage for normal sessions.
/// </summary>
public class AuthService
{
    private const string TokenKey     = "fs_jwt";
    private const string RememberKey  = "fs_remember";

    private readonly IJSRuntime _js;
    private string? _cachedToken; // in-memory mirror; avoids a JS hop on every API call

    public AuthService(IJSRuntime js) => _js = js;

    public async ValueTask<string?> GetTokenAsync()
    {
        if (_cachedToken is not null) return _cachedToken;
        // Persistent (remember-me) session stored in localStorage.
        _cachedToken = await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        if (_cachedToken is not null) return _cachedToken;
        // Normal session stored in sessionStorage.
        _cachedToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        return _cachedToken;
    }

    /// <param name="persistent">
    /// When true the JWT is written to localStorage (remember-me);
    /// when false it goes to sessionStorage only.
    /// </param>
    public async Task SetTokenAsync(string token, bool persistent = false)
    {
        _cachedToken = token;
        if (persistent)
        {
            await _js.InvokeVoidAsync("localStorage.setItem",    TokenKey, token);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
        }
        else
        {
            await _js.InvokeVoidAsync("sessionStorage.setItem",  TokenKey, token);
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        }
    }

    public async Task ClearAsync()
    {
        _cachedToken = null;
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem",   TokenKey);
    }

    public async ValueTask<bool> IsAuthenticatedAsync()
        => !string.IsNullOrEmpty(await GetTokenAsync());

    // -------------------------------------------------------------------------
    // Remember-me preference helpers
    //
    // The flag is saved in sessionStorage just before the OAuth redirect.
    // sessionStorage survives same-tab page navigations (including forceLoad),
    // so it is available when /auth/return loads after the OAuth round-trip.
    // -------------------------------------------------------------------------

    /// <summary>Saves the remember-me preference before the OAuth redirect.</summary>
    public async Task SetRememberAsync(bool remember)
        => await _js.InvokeVoidAsync("sessionStorage.setItem", RememberKey, remember ? "1" : "0");

    /// <summary>
    /// Reads and immediately clears the remember-me preference.
    /// Called from /auth/return after the OAuth flow completes.
    /// </summary>
    public async Task<bool> GetAndClearRememberAsync()
    {
        var val = await _js.InvokeAsync<string?>("sessionStorage.getItem", RememberKey);
        await _js.InvokeVoidAsync("sessionStorage.removeItem", RememberKey);
        return val == "1";
    }
}
