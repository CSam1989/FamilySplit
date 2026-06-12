using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Delete;

/// <summary>
/// Command — hard-deletes an expense (and its participants via cascade). Allowed only
/// on a non-Settled/Closed activity and a non-Locked expense, by the payer's family or
/// a global admin. Returns nothing (204).
/// </summary>
public sealed class DeleteExpenseCommandHandler
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<DeleteExpenseCommandHandler> _logger;

    public DeleteExpenseCommandHandler(
        AppDbContext db,
        AuditService audit,
        GroupMembershipGuard guard,
        ILogger<DeleteExpenseCommandHandler> logger)
    {
        _db = db;
        _audit = audit;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid expenseId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Deleting expense {ExpenseId} requested by user {UserId}", expenseId, callerId);

        var expense = await _db.Expenses.FindAsync([expenseId], ct)
            ?? throw ValidationErrors.NotFound("Expense not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == expense.ActivityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);
        await ExpenseGuards.RequireSameFamilyAsPayerOrGlobalAdminAsync(_db, expense.PaidByUserId, callerId, ct);

        if (activity.Status is ActivityStatus.Settled or ActivityStatus.Closed)
            throw ValidationErrors.Field("Status", "Cannot delete expenses from a closed or settled activity.");

        if (expense.Status == ExpenseStatus.Locked)
            throw ValidationErrors.Field("Status", "This expense is locked and cannot be deleted.");

        // Queue audit entry — persisted atomically with SaveChangesAsync below.
        _audit.Queue(callerId, "Expense", expenseId, "Deleted", new
        {
            activityId = expense.ActivityId,
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate = expense.ExpenseDate,
        });

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Expense {ExpenseId} deleted by user {UserId} — was '{Title}' {Amount} {Currency} on activity {ActivityId}",
            expenseId, callerId, expense.Title, expense.TotalAmount, expense.Currency, expense.ActivityId);
    }
}
