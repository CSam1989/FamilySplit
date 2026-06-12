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

namespace FamilySplit.Features.Expenses.Create;

/// <summary>
/// Command — creates an expense on an activity: seeds <c>ExpenseParticipant</c> rows
/// from the activity's participants, snapshots each weight at the expense date, and
/// runs <see cref="SplitCalculator"/>. Returns the new expense id.
/// </summary>
public sealed class CreateExpenseCommandHandler
{
    private readonly AppDbContext _db;
    private readonly CreateExpenseCommandValidator _validator;
    private readonly AuditService _audit;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<CreateExpenseCommandHandler> _logger;

    public CreateExpenseCommandHandler(
        AppDbContext db,
        CreateExpenseCommandValidator validator,
        AuditService audit,
        GroupMembershipGuard guard,
        ILogger<CreateExpenseCommandHandler> logger)
    {
        _db = db;
        _validator = validator;
        _audit = audit;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(Guid activityId, CreateExpenseCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating expense on activity {ActivityId} by user {UserId}", activityId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status is ActivityStatus.Settled or ActivityStatus.Closed)
            throw ValidationErrors.Field("Status", "Cannot add expenses to a closed or settled activity.");

        var currency = (cmd.Currency ?? "EUR").ToUpperInvariant();
        await ExpenseGuards.EnsureCurrencyConsistentAsync(_db, activityId, currency, excludeExpenseId: null, ct);
        await ExpenseGuards.EnsureCategoryValidAsync(_db, cmd.CategoryId, activity.GroupId, ct);

        var now = DateTimeOffset.UtcNow;
        var expenseDate = cmd.ExpenseDate;

        // Load activity participants with their member info for weight snapshotting.
        var participants = await (
            from ap in _db.ActivityParticipants
            join fm in _db.FamilyMembers on ap.FamilyMemberId equals fm.Id
            where ap.ActivityId == activityId
            select new { fm.Id, fm.DateOfBirth, fm.WeightOverride }
        ).ToListAsync(ct);

        _logger.LogDebug("Seeding {ParticipantCount} participants for new expense on activity {ActivityId}",
            participants.Count, activityId);

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            PaidByUserId = callerId,
            Title = cmd.Title.Trim(),
            Description = cmd.Description?.Trim(),
            TotalAmount = cmd.TotalAmount,
            Currency = currency,
            ExpenseDate = expenseDate,
            CategoryId = cmd.CategoryId,
            Status = ExpenseStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Expenses.Add(expense);

        // Seed ExpenseParticipants from ActivityParticipants, snapshotting weights.
        var expenseParticipants = participants.Select(p =>
        {
            var shell = new FamilyMember
            {
                Id = p.Id,
                DateOfBirth = p.DateOfBirth,
                WeightOverride = p.WeightOverride,
            };
            return new ExpenseParticipant
            {
                Id = Guid.NewGuid(),
                ExpenseId = expense.Id,
                FamilyMemberId = p.Id,
                WeightSnapshot = WeightCalculator.GetWeight(shell, expenseDate),
                IsExcluded = false,
            };
        }).ToList();

        _db.ExpenseParticipants.AddRange(expenseParticipants);

        // Calculate each participant's share.
        SplitCalculator.CalculateShares(expense.TotalAmount, expenseParticipants);

        // Queue audit entry — persisted atomically with SaveChangesAsync below.
        _audit.Queue(callerId, "Expense", expense.Id, "Created", new
        {
            activityId,
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate,
            participants = expenseParticipants.Count,
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Expense {ExpenseId} created on activity {ActivityId} by user {UserId} — '{Title}' {Amount} {Currency}",
            expense.Id, activityId, callerId, expense.Title, expense.TotalAmount, expense.Currency);

        return expense.Id;
    }
}
