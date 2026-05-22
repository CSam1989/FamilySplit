using Microsoft.JSInterop;

namespace FamilySplit.Client.Services;

/// <summary>
/// Thin wrapper over sessionStorage for the FamilySplit JWT.
/// JWTs live in sessionStorage so they're scoped to the current browser tab and
/// disappear when it closes — matching the blueprint's auth design.
/// </summary>
public class AuthService
{
    private const string TokenKey = "fs_jwt";

    private readonly IJSRuntime _js;
    private string? _cachedToken; // in-memory mirror; avoids a JS hop on every API call

    public AuthService(IJSRuntime js) => _js = js;

    public async ValueTask<string?> GetTokenAsync()
    {
        if (_cachedToken is not null) return _cachedToken;
        _cachedToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
        return _cachedToken;
    }

    public async Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        await _js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
    }

    public async Task ClearAsync()
    {
        _cachedToken = null;
        await _js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
    }

    public async ValueTask<bool> IsAuthenticatedAsync()
        => !string.IsNullOrEmpty(await GetTokenAsync());
}
