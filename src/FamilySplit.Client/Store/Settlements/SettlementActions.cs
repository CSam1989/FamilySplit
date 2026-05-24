using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Settlements;

// ── Load balances ─────────────────────────────────────────────────────────────
public record LoadBalancesAction(Guid GroupId, Guid ActivityId);
public record LoadBalancesSuccessAction(List<FamilyBalanceDto> Balances);
public record LoadBalancesFailureAction(string ErrorMessage);

// ── Load list ─────────────────────────────────────────────────────────────────
public record LoadSettlementsAction(Guid GroupId, Guid ActivityId);
public record LoadSettlementsSuccessAction(List<SettlementSummaryDto> Settlements);
public record LoadSettlementsFailureAction(string ErrorMessage);

// ── Generate settlements ──────────────────────────────────────────────────────
public record GenerateSettlementsAction(Guid GroupId, Guid ActivityId);
public record GenerateSettlementsSuccessAction(List<SettlementSummaryDto> Settlements);
public record GenerateSettlementsFailureAction(string ErrorMessage);

// ── Load detail ───────────────────────────────────────────────────────────────
public record LoadSettlementDetailAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record LoadSettlementDetailSuccessAction(SettlementDetailDto Settlement);
public record LoadSettlementDetailFailureAction(string ErrorMessage);

// ── Confirm sent ──────────────────────────────────────────────────────────────
public record ConfirmSentAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record ConfirmSentSuccessAction(SettlementDetailDto Settlement);
public record ConfirmSentFailureAction(string ErrorMessage);

// ── Confirm received ──────────────────────────────────────────────────────────
public record ConfirmReceivedAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record ConfirmReceivedSuccessAction(SettlementDetailDto Settlement);
public record ConfirmReceivedFailureAction(string ErrorMessage);

// ── Clear ─────────────────────────────────────────────────────────────────────
public record ClearSettlementsAction;
public record ClearSettlementErrorAction;
