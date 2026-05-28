using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Groups;

[FeatureState]
public record GroupState
{
    public IReadOnlyList<GroupSummaryDto> Groups { get; init; } = [];
    public GroupDetailDto? SelectedGroup { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
