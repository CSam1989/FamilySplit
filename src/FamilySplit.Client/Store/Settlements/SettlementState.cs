using Fluxor;
using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Settlements;

[FeatureState]
public record SettlementState
{
    public IReadOnlyList<SettlementSummaryDto> Settlements { get; init; } = [];
    public IReadOnlyList<FamilyBalanceDto> Balances { get; init; } = [];
    public SettlementDetailDto? SelectedSettlement { get; init; }
    public bool IsLoading { get; init; }
    public bool IsGenerating { get; init; }
    public string? ErrorMessage { get; init; }
}
