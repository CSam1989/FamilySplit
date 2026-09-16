using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Data;
using FamilySplit.Features.Expenses.Shared;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Delete;

/// <summary>
/// Command (business logic) — hard-deletes an expense (and its participants via cascade). Allowed
/// only on a non-Settled/Closed activity and a non-Locked expense, by the payer's family or a
/// global admin. Holds no EF — all data access goes through <see cref="IExpenseData"/> (ADR-001).
/// Returns nothing (204).
/// </summary>
public sealed class DeleteExpenseCommandHandler
{
    private readonly IExpenseData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<DeleteExpenseCommandHandler> _logger;

    public DeleteExpenseCommandHandler(
        IExpenseData data,
        IGroupMembershipGuard guard,
        ILogger<DeleteExpenseCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid expenseId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Deleting expense {ExpenseId} requested by user {UserId}", expenseId, callerId);

        var expense = await _data.GetExpenseAsync(expenseId, ct);
        if (expense is null)
        {
            _logger.LogDebug("Expense {ExpenseId} not found for delete requested by user {UserId}", expenseId, callerId);
            throw ValidationErrors.NotFound("Expense not found.");
        }

        var activity = await _data.GetActivityAsync(expense.ActivityId, ct);
        if (activity is null)
        {
            _logger.LogDebug("Activity {ActivityId} not found for expense {ExpenseId} delete", expense.ActivityId, expenseId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var ownership = await _data.GetExpenseOwnershipAsync(expense.PaidByUserId, callerId, ct);
        ExpenseGuards.RequireSameFamilyAsPayerOrGlobalAdmin(
            ownership.IsGlobalAdmin, ownership.CallerFamilyId, ownership.PayerFamilyId, expenseId, callerId, _logger);

        if (activity.Status is ActivityStatus.Settled or ActivityStatus.Closed)
        {
            _logger.LogDebug("Cannot delete expense {ExpenseId} — activity {ActivityId} has status {Status}",
                expenseId, expense.ActivityId, activity.Status);
            throw ValidationErrors.Field("Status", "Cannot delete expenses from a closed or settled activity.");
        }

        if (expense.Status == ExpenseStatus.Locked)
        {
            _logger.LogDebug("Cannot delete locked expense {ExpenseId}", expenseId);
            throw ValidationErrors.Field("Status", "This expense is locked and cannot be deleted.");
        }

        var audit = new AuditEntry(callerId, "Expense", expenseId, "Deleted", new
        {
            activityId = expense.ActivityId,
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate = expense.ExpenseDate,
        });

        await _data.DeleteExpenseAsync(expenseId, audit, ct);

        _logger.LogInformation(
            "Expense {ExpenseId} deleted by user {UserId} — was '{Title}' {Amount} {Currency} on activity {ActivityId}",
            expenseId, callerId, expense.Title, expense.TotalAmount, expense.Currency, expense.ActivityId);
    }
}
