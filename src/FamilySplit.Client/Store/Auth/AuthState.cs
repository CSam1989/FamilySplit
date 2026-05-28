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
    public string? Error { get; init; }
}
