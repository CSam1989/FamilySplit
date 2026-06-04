using FamilySplit.Application.Audit;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Expenses.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Expenses;

/// <summary>
/// Phase 5 — Expenses: create / update / delete expenses on activities,
/// auto-seed ExpenseParticipants from ActivityParticipants with weight snapshots,
/// and recalculate shares via SplitCalculator.
/// </summary>
public class ExpenseService
{
    private readonly AppDbContext _db;
    private readonly CreateExpenseValidator _createValidator;
    private readonly UpdateExpenseValidator _updateValidator;
    private readonly AuditService _audit;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(
        AppDbContext db,
        CreateExpenseValidator createValidator,
        UpdateExpenseValidator updateValidator,
        AuditService audit,
        ILogger<ExpenseService> logger)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _audit = audit;
        _logger = logger;
    }

    // ── List expenses for an activity ─────────────────────────────────────────

    public async Task<List<ExpenseSummaryDto>> ListAsync(Guid activityId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing expenses for activity {ActivityId} requested by user {UserId}",
            activityId, callerId);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var expenses = await _db.Expenses
            .Where(e => e.ActivityId == activityId)
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Select(e => new { e.Id, e.ActivityId, e.Title, e.Description, e.TotalAmount, e.Currency, e.ExpenseDate, e.PaidByUserId, e.Status, e.CreatedAt })
            .ToListAsync(ct);

        _logger.LogDebug("Found {ExpenseCount} expenses for activity {ActivityId}", expenses.Count, activityId);

        if (expenses.Count == 0) return [];

        var expenseIds = expenses.Select(e => e.Id).ToList();

        // Participant counts per expense.
        var participantCounts = await _db.ExpenseParticipants
            .Where(ep => expenseIds.Contains(ep.ExpenseId) && !ep.IsExcluded)
            .GroupBy(ep => ep.ExpenseId)
            .Select(g => new { ExpenseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExpenseId, x => x.Count, ct);

        // Payer info (User → FamilyMember → Family) for each unique payer.
        var payerUserIds = expenses.Select(e => e.PaidByUserId).Distinct().ToList();
        var payerInfo = await (
            from fm in _db.FamilyMembers
            join f in _db.Families on fm.FamilyId equals f.Id
            where fm.UserId != null && payerUserIds.Contains(fm.UserId.Value) && fm.IsActive
            select new { UserId = fm.UserId!.Value, fm.DisplayName, fm.FamilyId, FamilyName = f.Name }
        ).ToDictionaryAsync(x => x.UserId, ct);

        return expenses.Select(e =>
        {
            payerInfo.TryGetValue(e.PaidByUserId, out var payer);
            return new ExpenseSummaryDto(
                e.Id,
                e.ActivityId,
                e.Title,
                e.Description,
                e.TotalAmount,
                e.Currency,
                e.ExpenseDate,
                payer?.DisplayName ?? "Unknown",
                payer?.FamilyId ?? Guid.Empty,
                payer?.FamilyName ?? "Unknown",
                e.Status,
                participantCounts.GetValueOrDefault(e.Id, 0),
                e.CreatedAt);
        }).ToList();
    }

    // ── Get expense detail ────────────────────────────────────────────────────

    public async Task<ExpenseDetailDto> GetDetailAsync(Guid expenseId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting expense detail {ExpenseId} for user {UserId}", expenseId, callerId);

        var expense = await _db.Expenses
            .Where(e => e.Id == expenseId)
            .Select(e => new { e.Id, e.ActivityId, e.Title, e.Description, e.TotalAmount, e.Currency, e.ExpenseDate, e.PaidByUserId, e.Status, e.CreatedAt, e.UpdatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Expense not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == expense.ActivityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        return await BuildDetailDtoAsync(expense.Id, expense.ActivityId, expense.Title, expense.Description,
            expense.TotalAmount, expense.Currency, expense.ExpenseDate, expense.PaidByUserId,
            expense.Status, expense.CreatedAt, expense.UpdatedAt, ct);
    }

    // ── Create expense ────────────────────────────────────────────────────────

    public async Task<ExpenseDetailDto> CreateAsync(Guid activityId, CreateExpenseRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Creating expense on activity {ActivityId} by user {UserId} — title: {ExpenseTitle}, amount: {Amount}",
            activityId, callerId, req.Title, req.TotalAmount);

        await _createValidator.ValidateAndThrowAsync(req, ct);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status == ActivityStatus.Settled)
            throw Throw422("Status", "Cannot add expenses to a settled activity.");

        var now = DateTimeOffset.UtcNow;
        var expenseDate = req.ExpenseDate;

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
            Title = req.Title.Trim(),
            Description = req.Description?.Trim(),
            TotalAmount = req.TotalAmount,
            Currency = (req.Currency ?? "EUR").ToUpperInvariant(),
            ExpenseDate = expenseDate,
            CategoryId = req.CategoryId,
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

        return await BuildDetailDtoAsync(expense.Id, expense.ActivityId, expense.Title, expense.Description,
            expense.TotalAmount, expense.Currency, expense.ExpenseDate, expense.PaidByUserId,
            expense.Status, expense.CreatedAt, expense.UpdatedAt, ct);
    }

    // ── Update expense ────────────────────────────────────────────────────────

    public async Task<ExpenseDetailDto> UpdateAsync(Guid expenseId, UpdateExpenseRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("Updating expense {ExpenseId} by user {UserId}", expenseId, callerId);

        await _updateValidator.ValidateAndThrowAsync(req, ct);

        var expense = await _db.Expenses.FindAsync([expenseId], ct)
            ?? throw NotFound("Expense not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == expense.ActivityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status == ActivityStatus.Settled)
            throw Throw422("Status", "Cannot edit expenses on a settled activity.");

        if (expense.Status == ExpenseStatus.Locked)
            throw Throw422("Status", "This expense is locked and cannot be edited.");

        bool amountOrDateChanged = ExpenseReshuffleRequired.Check(expense.TotalAmount, req.TotalAmount, expense.ExpenseDate, req.ExpenseDate);

        // Capture before-state for audit diff.
        var before = new
        {
            title = expense.Title,
            amount = expense.TotalAmount,
            currency = expense.Currency,
            expenseDate = expense.ExpenseDate,
        };

        expense.Title = req.Title.Trim();
        expense.Description = req.Description?.Trim();
        expense.TotalAmount = req.TotalAmount;
        expense.Currency = (req.Currency ?? expense.Currency).ToUpperInvariant();
        expense.ExpenseDate = req.ExpenseDate;
        expense.CategoryId = req.CategoryId;
        expense.UpdatedAt = DateTimeOffset.UtcNow;

        // If amount or date changed, re-snapshot weights and recalculate shares.
        if (amountOrDateChanged)
        {
            _logger.LogDebug(
                "Amount or date changed on expense {ExpenseId} — re-snapshotting weights (old amount: {OldAmount}, new: {NewAmount})",
                expenseId, before.amount, req.TotalAmount);

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

        return await BuildDetailDtoAsync(expense.Id, expense.ActivityId, expense.Title, expense.Description,
            expense.TotalAmount, expense.Currency, expense.ExpenseDate, expense.PaidByUserId,
            expense.Status, expense.CreatedAt, expense.UpdatedAt, ct);
    }

    // ── Delete expense ────────────────────────────────────────────────────────

    public async Task DeleteAsync(Guid expenseId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("Deleting expense {ExpenseId} requested by user {UserId}", expenseId, callerId);

        var expense = await _db.Expenses.FindAsync([expenseId], ct)
            ?? throw NotFound("Expense not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == expense.ActivityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status == ActivityStatus.Settled)
            throw Throw422("Status", "Cannot delete expenses from a settled activity.");

        if (expense.Status == ExpenseStatus.Locked)
            throw Throw422("Status", "This expense is locked and cannot be deleted.");

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

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RequireGroupMemberAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        var callerFamilyId = await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException();

        var isMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId, ct);

        if (!isMember)
            throw new ForbiddenException();
    }

    private async Task<ExpenseDetailDto> BuildDetailDtoAsync(
        Guid id, Guid activityId, string title, string? description,
        decimal totalAmount, string currency, DateOnly expenseDate,
        Guid paidByUserId, ExpenseStatus status,
        DateTimeOffset createdAt, DateTimeOffset updatedAt,
        CancellationToken ct)
    {
        // Payer info.
        var payer = await (
            from fm in _db.FamilyMembers
            join f in _db.Families on fm.FamilyId equals f.Id
            where fm.UserId == paidByUserId && fm.IsActive
            select new { fm.DisplayName, fm.FamilyId, FamilyName = f.Name }
        ).FirstOrDefaultAsync(ct);

        // Participants with family info.
        var participants = await (
            from ep in _db.ExpenseParticipants
            join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
            join f in _db.Families on fm.FamilyId equals f.Id
            where ep.ExpenseId == id
            orderby f.Name, fm.DisplayName
            select new
            {
                ep.Id,
                ep.FamilyMemberId,
                fm.DisplayName,
                fm.FamilyId,
                FamilyName = f.Name,
                ep.WeightSnapshot,
                ep.CalculatedAmount,
                ep.IsExcluded,
            }
        ).ToListAsync(ct);

        var participantDtos = participants.Select(p => new ExpenseParticipantDto(
            p.Id,
            p.FamilyMemberId,
            p.DisplayName,
            p.FamilyId,
            p.FamilyName,
            p.WeightSnapshot,
            p.CalculatedAmount,
            p.IsExcluded)).ToList();

        return new ExpenseDetailDto(
            id,
            activityId,
            title,
            description,
            totalAmount,
            currency,
            expenseDate,
            payer?.DisplayName ?? "Unknown",
            payer?.FamilyId ?? Guid.Empty,
            payer?.FamilyName ?? "Unknown",
            status,
            participantDtos,
            createdAt,
            updatedAt);
    }

    private static ValidationException NotFound(string message) => new(message);
    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
