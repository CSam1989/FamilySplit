using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Auth;

[FeatureState]
public record AuthState
{
    public bool IsAuthenticated { get; init; }
    public bool IsGlobalAdmin { get; init; }
    public WhoAmIResponse? CurrentUser { get; init; }
    public bool IsLoading { get; init; }

    /// <summary>
    /// True once the silent-refresh auth check has run to completion at least once.
    /// Until this flips, the app must treat the auth state as "unknown" rather than
    /// "logged out" — otherwise guards redirect authenticated users before their
    /// session has resolved.
    /// </summary>
    public bool HasChecked { get; init; }

    public string? Error { get; init; }
}
