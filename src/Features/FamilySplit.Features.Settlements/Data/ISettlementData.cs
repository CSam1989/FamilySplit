using FamilySplit.Common.Auditing;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Shared;

namespace FamilySplit.Features.Settlements.Data;

/// <summary>
/// The Settlements slice data-access seam (ADR-001). All write-side EF/<c>AppDbContext</c> access
/// lives behind this interface so the command handlers (Generate / ConfirmSent / ConfirmReceived)
/// hold no EF and are unit-tested by mocking it. Reads return plain records — never tracked
/// entities. Writes accept the command handler's computed state and persist it atomically,
/// including the audit flush (financial mutations) and the post-save family notification.
/// The implementation is Testcontainers-tested.
/// </summary>
public interface ISettlementData
{
    // ── reads the commands need (plain records, no tracking leaks) ────────────────

    /// <summary>The activity's core fields (group, status, parent), or null if it does not exist.</summary>
    Task<ActivityForSettlement?> GetActivityAsync(Guid activityId, CancellationToken ct);

    /// <summary>The number of settlement rows already persisted for the activity (idempotency check).</summary>
    Task<int> CountSettlementsAsync(Guid activityId, CancellationToken ct);

    /// <summary>The dominant currency across the activity and its sub-activities' expenses (defaults to EUR).</summary>
    Task<string> GetActivityCurrencyAsync(Guid activityId, CancellationToken ct);

    /// <summary>The expense + participant inputs needed to compute per-family balances.</summary>
    Task<SettlementBalanceInputs> GetBalanceInputsAsync(Guid activityId, CancellationToken ct);

    /// <summary>The settlement's fields + its activity's group, or null if it does not exist.</summary>
    Task<SettlementForConfirm?> GetSettlementForConfirmAsync(Guid settlementId, CancellationToken ct);

    /// <summary>True when every settlement on the activity other than the excluded one is Completed.</summary>
    Task<bool> AreOtherSettlementsCompletedAsync(Guid activityId, Guid excludeSettlementId, CancellationToken ct);

    // ── writes (each owns SaveChangesAsync + the atomic audit flush) ──────────────

    /// <summary>Transitions the activity to Settled (the zero-balance generate path).</summary>
    Task MarkActivitySettledAsync(Guid activityId, CancellationToken ct);

    /// <summary>
    /// Persists the generated settlement rows together with one audit entry each (atomic). A concurrent
    /// generation that won the unique-index race is swallowed (idempotent) rather than surfaced.
    /// </summary>
    Task GenerateSettlementsAsync(Guid activityId, IReadOnlyList<Settlement> settlements, IReadOnlyList<AuditEntry> audits, CancellationToken ct);

    /// <summary>
    /// Marks the settlement PayerSent, records the approval step + audit (atomic), then notifies the
    /// receiver family.
    /// </summary>
    Task ConfirmSentAsync(Guid settlementId, ApprovalStep step, AuditEntry audit, SettlementNotification notify, CancellationToken ct);

    /// <summary>
    /// Marks the settlement Completed (with <paramref name="completedAt"/>), records the approval step +
    /// audit, optionally transitions the activity to Settled (atomic), then notifies the payer family.
    /// </summary>
    Task ConfirmReceivedAsync(
        Guid settlementId,
        Guid activityId,
        DateTimeOffset completedAt,
        ApprovalStep step,
        AuditEntry audit,
        bool markActivitySettled,
        SettlementNotification notify,
        CancellationToken ct);
}

/// <summary>The activity fields the settlement commands guard on: its group, lifecycle status and parent.</summary>
public sealed record ActivityForSettlement(Guid GroupId, ActivityStatus Status, Guid? ParentActivityId);

/// <summary>The expense + participant rows used to compute per-family balances.</summary>
public sealed record SettlementBalanceInputs(
    IReadOnlyList<BalanceCalculator.ExpenseData> Expenses,
    IReadOnlyList<BalanceCalculator.ParticipantData> Participants);

/// <summary>A settlement's fields (plus its activity's group) needed by the confirm commands.</summary>
public sealed record SettlementForConfirm(
    Guid Id,
    Guid ActivityId,
    Guid GroupId,
    Guid PayerFamilyId,
    Guid ReceiverFamilyId,
    decimal Amount,
    string Currency,
    SettlementStatus Status);

/// <summary>The family notification a confirm command wants dispatched after the mutation is saved.</summary>
public sealed record SettlementNotification(Guid TargetFamilyId, string Title, string Message, string Url);
