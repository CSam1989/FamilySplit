using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Settlements;

[FeatureState]
public record SettlementState
{
    // Activity-scoped (used by ActivityDetail)
    public IReadOnlyList<SettlementSummaryDto> Settlements { get; init; } = [];
    public IReadOnlyList<FamilyBalanceDto> Balances { get; init; } = [];
    public SettlementDetailDto? SelectedSettlement { get; init; }
    public bool IsLoading { get; init; }
    public bool IsGenerating { get; init; }
    public string? ErrorMessage { get; init; }

    // Group-scoped (used by GroupDetail)
    public IReadOnlyList<GroupSettlementSummaryDto> GroupSettlements { get; init; } = [];
    public bool IsLoadingGroupSettlements { get; init; }

    // Cross-group (used by main dashboard — all pending settlements for the caller)
    public IReadOnlyList<GroupSettlementSummaryDto> MyPendingSettlements { get; init; } = [];
    public bool IsLoadingMyPending { get; init; }
}
