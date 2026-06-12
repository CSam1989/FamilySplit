using FamilySplit.Common.Auditing;
using FamilySplit.Common.Calculations;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Shared;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Update;

/// <summary>
/// Command — edits an expense. If the amount or date changed, re-snapshots every
/// participant's weight and recalculates shares. Returns nothing (204).
/// </summary>
public sealed class UpdateExpenseCommandHandler
{
    private readonly AppDbContext _db;
    private readonly UpdateExpenseCommandValidator _validator;
    private readonly AuditService _audit;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<UpdateExpenseCommandHandler> _logger;

    public UpdateExpenseCommandHandler(
        AppDbContext db,
        UpdateExpenseCommandValidator validator,
        AuditService audit,
        GroupMembershipGuard guard,
        ILogger<UpdateExpenseCommandHandler> logger)
    {
        _db = db;
        _validator = validator;
        _audit = audit;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid expenseId, UpdateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Updating expense {ExpenseId} by user {UserId}", expenseId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

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
            throw ValidationErrors.Field("Status", "Cannot edit expenses on a closed or settled activity.");

        if (expense.Status == ExpenseStatus.Locked)
            throw ValidationErrors.Field("Status", "This expense is locked and cannot be edited.");

        var currency = (cmd.Currency ?? expense.Currency).ToUpperInvariant();
        await ExpenseGuards.EnsureCurrencyConsistentAsync(_db, expense.ActivityId, currency, excludeExpenseId: expenseId, ct);
        await ExpenseGuards.EnsureCategoryValidAsync(_db, cmd.CategoryId, activity.GroupId, ct);

        bool amountOrDateChanged = ExpenseReshuffleRequired.Check(expense.TotalAmount, cmd.TotalAmount, expense.ExpenseDate, cmd.ExpenseDate);

        // Capture before-state for audit diff.
        var before = new
        {
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate = expense.ExpenseDate,
        };

        expense.Title = cmd.Title.Trim();
        expense.Description = cmd.Description?.Trim();
        expense.TotalAmount = cmd.TotalAmount;
        expense.Currency = currency;
        expense.ExpenseDate = cmd.ExpenseDate;
        expense.CategoryId = cmd.CategoryId;
        expense.UpdatedAt = DateTimeOffset.UtcNow;

        // If amount or date changed, re-snapshot weights and recalculate shares.
        if (amountOrDateChanged)
        {
            _logger.LogDebug(
                "Amount or date changed on expense {ExpenseId} — re-snapshotting weights",
                expenseId);

            var existingParticipants = await _db.ExpenseParticipants
                .Where(ep => ep.ExpenseId == expenseId)
                .ToListAsync(ct);

            if (existingParticipants.Count > 0)
            {
                var memberIds = existingParticipants.Select(ep => ep.FamilyMemberId).ToList();
                var memberData = await _db.FamilyMembers
                    .Where(fm => memberIds.Contains(fm.Id))
                    .Select(fm => new { fm.Id, fm.DateOfBirth, fm.WeightOverride })
                    .ToDictionaryAsync(m => m.Id, ct);

                foreach (var ep in existingParticipants)
                {
                    if (memberData.TryGetValue(ep.FamilyMemberId, out var m))
                    {
                        var shell = new FamilyMember
                        {
                            Id = m.Id,
                            DateOfBirth = m.DateOfBirth,
                            WeightOverride = m.WeightOverride,
                        };
                        ep.WeightSnapshot = WeightCalculator.GetWeight(shell, expense.ExpenseDate);
                    }
                }

                SplitCalculator.CalculateShares(expense.TotalAmount, existingParticipants);
            }
        }

        // Queue audit entry — persisted atomically with SaveChangesAsync below.
        _audit.Queue(callerId, "Expense", expenseId, "Updated", new
        {
            before,
            after = new
            {
                title = expense.Title,
                amount = expense.TotalAmount,
                currency = expense.Currency,
                expenseDate = expense.ExpenseDate,
            },
            recalculated = amountOrDateChanged,
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Expense {ExpenseId} updated by user {UserId} — '{Title}' {Amount} {Currency}",
            expenseId, callerId, expense.Title, expense.TotalAmount, expense.Currency);
    }
}
