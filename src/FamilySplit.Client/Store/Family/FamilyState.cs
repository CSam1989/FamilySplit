using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Family;

[FeatureState]
public record FamilyState
{
    /// <summary>The caller's own Family (loaded on demand).</summary>
    public FamilyDto? MyFamily { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
