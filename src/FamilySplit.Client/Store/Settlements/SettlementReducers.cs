using Fluxor;

namespace FamilySplit.Client.Store.Settlements;

public static class SettlementReducers
{
    // ── Load balances ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadBalancesAction))]
    public static SettlementState OnLoadBalances(SettlementState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static SettlementState OnLoadBalancesSuccess(SettlementState state, LoadBalancesSuccessAction action) =>
        state with { IsLoading = false, Balances = action.Balances };

    [ReducerMethod]
    public static SettlementState OnLoadBalancesFailure(SettlementState state, LoadBalancesFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Load list ─────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadSettlementsAction))]
    public static SettlementState OnLoad(SettlementState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static SettlementState OnLoadSuccess(SettlementState state, LoadSettlementsSuccessAction action) =>
        state with { IsLoading = false, Settlements = action.Settlements };

    [ReducerMethod]
    public static SettlementState OnLoadFailure(SettlementState state, LoadSettlementsFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Generate ──────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(GenerateSettlementsAction))]
    public static SettlementState OnGenerate(SettlementState state) =>
        state with { IsGenerating = true, ErrorMessage = null };

    [ReducerMethod]
    public static SettlementState OnGenerateSuccess(SettlementState state, GenerateSettlementsSuccessAction action) =>
        state with { IsGenerating = false, Settlements = action.Settlements };

    [ReducerMethod]
    public static SettlementState OnGenerateFailure(SettlementState state, GenerateSettlementsFailureAction action) =>
        state with { IsGenerating = false, ErrorMessage = action.ErrorMessage };

    // ── Load detail ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadSettlementDetailAction))]
    public static SettlementState OnLoadDetail(SettlementState state) =>
        state with { IsLoading = true, ErrorMessage = null, SelectedSettlement = null };

    [ReducerMethod]
    public static SettlementState OnLoadDetailSuccess(SettlementState state, LoadSettlementDetailSuccessAction action) =>
        state with { IsLoading = false, SelectedSettlement = action.Settlement };

    [ReducerMethod]
    public static SettlementState OnLoadDetailFailure(SettlementState state, LoadSettlementDetailFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Confirm sent ──────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ConfirmSentAction))]
    public static SettlementState OnConfirmSent(SettlementState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static SettlementState OnConfirmSentSuccess(SettlementState state, ConfirmSentSuccessAction action) =>
        state with
        {
            IsLoading = false,
            SelectedSettlement = action.Settlement,
            Settlements = state.Settlements
                .Select(s => s.Id == action.Settlement.Id
                    ? s with { Status = action.Settlement.Status }
                    : s)
                .ToList(),
        };

    [ReducerMethod]
    public static SettlementState OnConfirmSentFailure(SettlementState state, ConfirmSentFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Confirm received ──────────────────────────────────────────────────────

    [ReducerMethod(typeof(ConfirmReceivedAction))]
    public static SettlementState OnConfirmReceived(SettlementState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static SettlementState OnConfirmReceivedSuccess(SettlementState state, ConfirmReceivedSuccessAction action) =>
        state with
        {
            IsLoading = false,
            SelectedSettlement = action.Settlement,
            Settlements = state.Settlements
                .Select(s => s.Id == action.Settlement.Id
                    ? s with { Status = action.Settlement.Status, CompletedAt = action.Settlement.CompletedAt }
                    : s)
                .ToList(),
        };

    [ReducerMethod]
    public static SettlementState OnConfirmReceivedFailure(SettlementState state, ConfirmReceivedFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear ─────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearSettlementsAction))]
    public static SettlementState OnClear(SettlementState state) =>
        state with { Settlements = [], Balances = [], SelectedSettlement = null, ErrorMessage = null };

    [ReducerMethod(typeof(ClearSettlementErrorAction))]
    public static SettlementState OnClearError(SettlementState state) =>
        state with { ErrorMessage = null };
}
