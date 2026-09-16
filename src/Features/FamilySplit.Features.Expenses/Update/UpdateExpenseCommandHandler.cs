using FamilySplit.Common.Auditing;
using FamilySplit.Common.Calculations;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Expenses.Data;
using FamilySplit.Features.Expenses.Shared;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Update;

/// <summary>
/// Command (business logic) — edits an expense. If the amount or date changed, re-snapshots every
/// participant's weight and recalculates shares. Holds no EF — all data access goes through
/// <see cref="IExpenseData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateExpenseCommandHandler
{
    private readonly IExpenseData _data;
    private readonly UpdateExpenseCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<UpdateExpenseCommandHandler> _logger;

    public UpdateExpenseCommandHandler(
        IExpenseData data,
        UpdateExpenseCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<UpdateExpenseCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid expenseId, UpdateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Updating expense {ExpenseId} by user {UserId}", expenseId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var expense = await _data.GetExpenseAsync(expenseId, ct);
        if (expense is null)
        {
            _logger.LogDebug("Expense {ExpenseId} not found for update by user {UserId}", expenseId, callerId);
            throw ValidationErrors.NotFound("Expense not found.");
        }

        var activity = await _data.GetActivityAsync(expense.ActivityId, ct);
        if (activity is null)
        {
            _logger.LogDebug("Activity {ActivityId} not found for expense {ExpenseId} update", expense.ActivityId, expenseId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var ownership = await _data.GetExpenseOwnershipAsync(expense.PaidByUserId, callerId, ct);
        ExpenseGuards.RequireSameFamilyAsPayerOrGlobalAdmin(
            ownership.IsGlobalAdmin, ownership.CallerFamilyId, ownership.PayerFamilyId, expenseId, callerId, _logger);

        if (activity.Status is ActivityStatus.Settled or ActivityStatus.Closed)
        {
            _logger.LogDebug("Cannot edit expense {ExpenseId} — activity {ActivityId} has status {Status}",
                expenseId, expense.ActivityId, activity.Status);
            throw ValidationErrors.Field("Status", "Cannot edit expenses on a closed or settled activity.");
        }

        if (expense.Status == ExpenseStatus.Locked)
        {
            _logger.LogDebug("Cannot edit locked expense {ExpenseId}", expenseId);
            throw ValidationErrors.Field("Status", "This expense is locked and cannot be edited.");
        }

        var currency = (cmd.Currency ?? expense.Currency).ToUpperInvariant();
        ExpenseGuards.EnsureCurrencyConsistent(
            await _data.GetActivityCurrencyAsync(expense.ActivityId, excludeExpenseId: expenseId, ct), currency, expense.ActivityId, _logger);

        if (cmd.CategoryId is not null)
            ExpenseGuards.EnsureCategoryValid(
                await _data.CategoryIsValidForGroupAsync(cmd.CategoryId.Value, activity.GroupId, ct), cmd.CategoryId.Value, _logger);

        bool amountOrDateChanged = ExpenseReshuffleRequired.Check(
            expense.TotalAmount, cmd.TotalAmount, expense.ExpenseDate, cmd.ExpenseDate);

        // Capture before-state for the audit diff.
        var before = new
        {
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate = expense.ExpenseDate,
        };

        var fields = new ExpenseFields(
            cmd.Title.Trim(), cmd.Description?.Trim(), cmd.TotalAmount, currency, cmd.ExpenseDate, cmd.CategoryId);

        // If amount or date changed, re-snapshot weights and recalculate shares.
        List<ParticipantShare>? recomputed = null;
        if (amountOrDateChanged)
        {
            _logger.LogDebug(
                "Amount or date changed on expense {ExpenseId} — re-snapshotting weights", expenseId);

            var inputs = await _data.GetExpenseParticipantsAsync(expenseId, ct);
            if (inputs.Count > 0)
            {
                var rebuilt = inputs.Select(p =>
                {
                    var shell = new FamilyMember
                    {
                        Id = p.FamilyMemberId,
                        DateOfBirth = p.DateOfBirth,
                        WeightOverride = p.WeightOverride,
                    };
                    return new ExpenseParticipant
                    {
                        Id = p.ParticipantId,
                        FamilyMemberId = p.FamilyMemberId,
                        WeightSnapshot = WeightCalculator.GetWeight(shell, cmd.ExpenseDate),
                        IsExcluded = p.IsExcluded,
                    };
                }).ToList();

                SplitCalculator.CalculateShares(cmd.TotalAmount, rebuilt);

                recomputed = rebuilt
                    .Select(r => new ParticipantShare(r.Id, r.WeightSnapshot, r.CalculatedAmount))
                    .ToList();
            }
        }

        var audit = new AuditEntry(callerId, "Expense", expenseId, "Updated", new
        {
            before,
            after = new
            {
                title = fields.Title,
                amount = fields.TotalAmount,
                currency = fields.Currency,
                expenseDate = fields.ExpenseDate,
            },
            recalculated = amountOrDateChanged,
        });

        await _data.UpdateExpenseAsync(expenseId, fields, recomputed, audit, ct);

        _logger.LogInformation(
            "Expense {ExpenseId} updated by user {UserId} — '{Title}' {Amount} {Currency}",
            expenseId, callerId, fields.Title, fields.TotalAmount, fields.Currency);
    }
}
