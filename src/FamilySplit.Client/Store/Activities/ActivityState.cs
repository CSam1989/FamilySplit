using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Activities;

[FeatureState]
public record ActivityState
{
    public IReadOnlyList<ActivitySummaryDto> Activities { get; init; } = [];
    public ActivityDetailDto? SelectedActivity { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
