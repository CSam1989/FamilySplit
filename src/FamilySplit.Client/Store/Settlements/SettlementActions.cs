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
// Strict CQRS: the command returns 204; success carries no payload — the effect re-queries the list.
public record GenerateSettlementsAction(Guid GroupId, Guid ActivityId);
public record GenerateSettlementsSuccessAction;
public record GenerateSettlementsFailureAction(string ErrorMessage);

// ── Load detail ───────────────────────────────────────────────────────────────
public record LoadSettlementDetailAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record LoadSettlementDetailSuccessAction(SettlementDetailDto Settlement);
public record LoadSettlementDetailFailureAction(string ErrorMessage);

// ── Confirm sent ──────────────────────────────────────────────────────────────
// Strict CQRS: 204; success carries no payload — the effect re-queries list + detail + group/pending.
public record ConfirmSentAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record ConfirmSentSuccessAction;
public record ConfirmSentFailureAction(string ErrorMessage);

// ── Confirm received ──────────────────────────────────────────────────────────
public record ConfirmReceivedAction(Guid GroupId, Guid ActivityId, Guid SettlementId);
public record ConfirmReceivedSuccessAction;
public record ConfirmReceivedFailureAction(string ErrorMessage);

// ── Load group-level settlements ──────────────────────────────────────────────
public record LoadGroupSettlementsAction(Guid GroupId);
public record LoadGroupSettlementsSuccessAction(List<GroupSettlementSummaryDto> Settlements);
public record LoadGroupSettlementsFailureAction(string ErrorMessage);
public record ClearGroupSettlementsAction;

// ── Load dashboard (cross-group) pending settlements ─────────────────────────
public record LoadMyPendingSettlementsAction;
public record LoadMyPendingSettlementsSuccessAction(List<GroupSettlementSummaryDto> Settlements);
public record LoadMyPendingSettlementsFailureAction(string ErrorMessage);

// ── Clear ─────────────────────────────────────────────────────────────────────
public record ClearSettlementsAction;
public record ClearSettlementErrorAction;
