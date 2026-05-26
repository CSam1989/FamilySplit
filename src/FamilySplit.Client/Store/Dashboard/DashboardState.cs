using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Dashboard;

[FeatureState]
public record DashboardState
{
    public IReadOnlyList<DashboardGroupStatDto> Stats { get; init; } = [];
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
