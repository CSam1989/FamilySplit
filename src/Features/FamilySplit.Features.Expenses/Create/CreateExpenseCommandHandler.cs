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

namespace FamilySplit.Features.Expenses.Create;

/// <summary>
/// Command (business logic) — creates an expense on an activity: seeds <c>ExpenseParticipant</c>
/// rows from the activity's participants, snapshots each weight at the expense date, and runs
/// <see cref="SplitCalculator"/>. Holds no EF — all data access goes through <see cref="IExpenseData"/>
/// (ADR-001). Returns the new expense id.
/// </summary>
public sealed class CreateExpenseCommandHandler
{
    private readonly IExpenseData _data;
    private readonly CreateExpenseCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<CreateExpenseCommandHandler> _logger;

    public CreateExpenseCommandHandler(
        IExpenseData data,
        CreateExpenseCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<CreateExpenseCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(Guid activityId, CreateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating expense on activity {ActivityId} by user {UserId}", activityId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var activity = await _data.GetActivityAsync(activityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status is ActivityStatus.Settled or ActivityStatus.Closed)
            throw ValidationErrors.Field("Status", "Cannot add expenses to a closed or settled activity.");

        var currency = (cmd.Currency ?? "EUR").ToUpperInvariant();
        ExpenseGuards.EnsureCurrencyConsistent(
            await _data.GetActivityCurrencyAsync(activityId, excludeExpenseId: null, ct), currency);

        if (cmd.CategoryId is not null)
            ExpenseGuards.EnsureCategoryValid(
                await _data.CategoryIsValidForGroupAsync(cmd.CategoryId.Value, activity.GroupId, ct));

        var expenseId = Guid.NewGuid();
        var expenseDate = cmd.ExpenseDate;

        // Load activity participants with their member info for weight snapshotting.
        var snapshotInputs = await _data.GetActivityParticipantsAsync(activityId, ct);

        _logger.LogDebug("Seeding {ParticipantCount} participants for new expense on activity {ActivityId}",
            snapshotInputs.Count, activityId);

        var expense = new Expense
        {
            Id = expenseId,
            ActivityId = activityId,
            PaidByUserId = callerId,
            Title = cmd.Title.Trim(),
            Description = cmd.Description?.Trim(),
            TotalAmount = cmd.TotalAmount,
            Currency = currency,
            ExpenseDate = expenseDate,
            CategoryId = cmd.CategoryId,
            Status = ExpenseStatus.Active,
        };

        // Seed ExpenseParticipants from the activity's participants, snapshotting weights.
        var participants = snapshotInputs.Select(p =>
        {
            var shell = new FamilyMember
            {
                Id = p.FamilyMemberId,
                DateOfBirth = p.DateOfBirth,
                WeightOverride = p.WeightOverride,
            };
            return new ExpenseParticipant
            {
                Id = Guid.NewGuid(),
                ExpenseId = expenseId,
                FamilyMemberId = p.FamilyMemberId,
                WeightSnapshot = WeightCalculator.GetWeight(shell, expenseDate),
                IsExcluded = false,
            };
        }).ToList();

        // Calculate each participant's share.
        SplitCalculator.CalculateShares(expense.TotalAmount, participants);

        var audit = new AuditEntry(callerId, "Expense", expenseId, "Created", new
        {
            activityId,
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate,
            participants = participants.Count,
        });

        await _data.AddExpenseAsync(expense, participants, audit, ct);

        _logger.LogInformation(
            "Expense {ExpenseId} created on activity {ActivityId} by user {UserId} — '{Title}' {Amount} {Currency}",
            expenseId, activityId, callerId, expense.Title, expense.TotalAmount, expense.Currency);

        return expenseId;
    }
}
