using FamilySplit.Common.Auditing;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Expenses.Data;

/// <summary>
/// The Expenses slice data-access seam (ADR-001). All EF/<c>AppDbContext</c> access for the
/// write side lives behind this interface so the command handlers (business logic) hold no EF
/// and are unit-tested by mocking it. Reads return plain records — never tracked entities — so
/// tracking never leaks into business logic. Writes accept the computed state and persist it
/// atomically (including the audit flush). The implementation is Testcontainers-tested.
/// </summary>
public interface IExpenseData
{
    // ── reads the commands need (plain records) ──────────────────────────────────
    Task<ActivityForExpense?> GetActivityAsync(Guid activityId, CancellationToken ct);

    /// <summary>The currency already in use on the activity (first existing expense), or null.</summary>
    Task<string?> GetActivityCurrencyAsync(Guid activityId, Guid? excludeExpenseId, CancellationToken ct);

    /// <summary>True if the category exists and is system-wide or scoped to the group.</summary>
    Task<bool> CategoryIsValidForGroupAsync(Guid categoryId, Guid groupId, CancellationToken ct);

    /// <summary>The activity's participants with the fields needed to snapshot weights.</summary>
    Task<IReadOnlyList<ParticipantSnapshotInput>> GetActivityParticipantsAsync(Guid activityId, CancellationToken ct);

    Task<ExpenseSnapshot?> GetExpenseAsync(Guid expenseId, CancellationToken ct);

    /// <summary>The facts needed to enforce the payer-family / global-admin ownership guard.</summary>
    Task<ExpenseOwnership> GetExpenseOwnershipAsync(Guid payerUserId, Guid callerId, CancellationToken ct);

    /// <summary>The expense's participants with the fields needed to re-snapshot weights.</summary>
    Task<IReadOnlyList<ParticipantReshuffleInput>> GetExpenseParticipantsAsync(Guid expenseId, CancellationToken ct);

    // ── writes (each owns SaveChangesAsync + the atomic audit flush) ──────────────
    Task AddExpenseAsync(Expense expense, IReadOnlyList<ExpenseParticipant> participants, AuditEntry audit, CancellationToken ct);

    Task UpdateExpenseAsync(Guid expenseId, ExpenseFields fields, IReadOnlyList<ParticipantShare>? recomputed, AuditEntry audit, CancellationToken ct);

    Task DeleteExpenseAsync(Guid expenseId, AuditEntry audit, CancellationToken ct);
}

/// <summary>The activity fields a command needs: its group and lifecycle status.</summary>
public sealed record ActivityForExpense(Guid GroupId, ActivityStatus Status);

/// <summary>A current expense's fields needed by the Update/Delete commands.</summary>
public sealed record ExpenseSnapshot(
    Guid Id,
    Guid ActivityId,
    Guid PaidByUserId,
    string Title,
    decimal TotalAmount,
    string Currency,
    DateOnly ExpenseDate,
    ExpenseStatus Status);

/// <summary>Ownership facts for the payer-family / global-admin guard.</summary>
public sealed record ExpenseOwnership(bool IsGlobalAdmin, Guid? CallerFamilyId, Guid? PayerFamilyId);

/// <summary>An activity participant's weight inputs, used when seeding a new expense.</summary>
public sealed record ParticipantSnapshotInput(Guid FamilyMemberId, DateOnly? DateOfBirth, decimal? WeightOverride);

/// <summary>An expense participant's weight inputs, used when re-snapshotting on update.</summary>
public sealed record ParticipantReshuffleInput(
    Guid ParticipantId,
    Guid FamilyMemberId,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsExcluded);

/// <summary>The new expense field values to persist on update.</summary>
public sealed record ExpenseFields(
    string Title,
    string? Description,
    decimal TotalAmount,
    string Currency,
    DateOnly ExpenseDate,
    Guid? CategoryId);

/// <summary>A recomputed participant share to persist on update.</summary>
public sealed record ParticipantShare(Guid ParticipantId, decimal WeightSnapshot, decimal CalculatedAmount);
