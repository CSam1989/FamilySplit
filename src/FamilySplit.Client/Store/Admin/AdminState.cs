using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Admin;

[FeatureState]
public record AdminState
{
    public IReadOnlyList<FamilyDto> Families { get; init; } = [];
    public FamilyDto? SelectedFamily { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
