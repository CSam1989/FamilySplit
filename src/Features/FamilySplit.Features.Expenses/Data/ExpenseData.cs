using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Data;

/// <summary>
/// The Expenses write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain records; writes
/// accept the command handler's computed state and persist it together with the audit row
/// (queued on the same context, flushed in one <c>SaveChangesAsync</c> — atomic). Verified with
/// Testcontainers through the expense endpoints.
/// </summary>
internal sealed class ExpenseData : IExpenseData
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<ExpenseData> _logger;

    public ExpenseData(AppDbContext db, AuditService audit, ILogger<ExpenseData> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ActivityForExpense?> GetActivityAsync(Guid activityId, CancellationToken ct) =>
        await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new ActivityForExpense(a.GroupId, a.Status))
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetActivityCurrencyAsync(Guid activityId, Guid? excludeExpenseId, CancellationToken ct) =>
        await _db.Expenses
            .AsNoTracking()
            .Where(e => e.ActivityId == activityId && (excludeExpenseId == null || e.Id != excludeExpenseId))
            .Select(e => e.Currency)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> CategoryIsValidForGroupAsync(Guid categoryId, Guid groupId, CancellationToken ct) =>
        await _db.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id == categoryId && (c.GroupId == null || c.GroupId == groupId), ct);

    public async Task<IReadOnlyList<ParticipantSnapshotInput>> GetActivityParticipantsAsync(Guid activityId, CancellationToken ct) =>
        await (
            from ap in _db.ActivityParticipants.AsNoTracking()
            join fm in _db.FamilyMembers on ap.FamilyMemberId equals fm.Id
            where ap.ActivityId == activityId
            select new ParticipantSnapshotInput(fm.Id, fm.DateOfBirth, fm.WeightOverride)
        ).ToListAsync(ct);

    public async Task<ExpenseSnapshot?> GetExpenseAsync(Guid expenseId, CancellationToken ct) =>
        await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == expenseId)
            .Select(e => new ExpenseSnapshot(
                e.Id, e.ActivityId, e.PaidByUserId, e.Title, e.TotalAmount, e.Currency, e.ExpenseDate, e.Status))
            .FirstOrDefaultAsync(ct);

    public async Task<ExpenseOwnership> GetExpenseOwnershipAsync(Guid payerUserId, Guid callerId, CancellationToken ct)
    {
        var isGlobalAdmin = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync(ct);

        var callerFamilyId = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        // Not filtered by IsActive — the family that fronted the money is the same regardless
        // of whether that member has since been deactivated.
        var payerFamilyId = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == payerUserId)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        return new ExpenseOwnership(isGlobalAdmin, callerFamilyId, payerFamilyId);
    }

    public async Task<IReadOnlyList<ParticipantReshuffleInput>> GetExpenseParticipantsAsync(Guid expenseId, CancellationToken ct) =>
        await (
            from ep in _db.ExpenseParticipants.AsNoTracking()
            join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
            where ep.ExpenseId == expenseId
            select new ParticipantReshuffleInput(ep.Id, ep.FamilyMemberId, fm.DateOfBirth, fm.WeightOverride, ep.IsExcluded)
        ).ToListAsync(ct);

    public async Task AddExpenseAsync(Expense expense, IReadOnlyList<ExpenseParticipant> participants, AuditEntry audit, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        expense.CreatedAt = now;
        expense.UpdatedAt = now;

        _db.Expenses.Add(expense);
        _db.ExpenseParticipants.AddRange(participants);
        _audit.Queue(audit);

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateExpenseAsync(Guid expenseId, ExpenseFields fields, IReadOnlyList<ParticipantShare>? recomputed, AuditEntry audit, CancellationToken ct)
    {
        var expense = await _db.Expenses.FindAsync([expenseId], ct);
        if (expense is null)
        {
            _logger.LogDebug("Expense {ExpenseId} not found when persisting update", expenseId);
            throw ValidationErrors.NotFound("Expense not found.");
        }

        expense.Title = fields.Title;
        expense.Description = fields.Description;
        expense.TotalAmount = fields.TotalAmount;
        expense.Currency = fields.Currency;
        expense.ExpenseDate = fields.ExpenseDate;
        expense.CategoryId = fields.CategoryId;
        expense.UpdatedAt = DateTimeOffset.UtcNow;

        if (recomputed is { Count: > 0 })
        {
            var ids = recomputed.Select(r => r.ParticipantId).ToList();
            var tracked = await _db.ExpenseParticipants
                .Where(ep => ids.Contains(ep.Id))
                .ToListAsync(ct);

            var byId = recomputed.ToDictionary(r => r.ParticipantId);
            foreach (var ep in tracked)
            {
                if (byId.TryGetValue(ep.Id, out var share))
                {
                    ep.WeightSnapshot = share.WeightSnapshot;
                    ep.CalculatedAmount = share.CalculatedAmount;
                }
            }
        }

        _audit.Queue(audit);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteExpenseAsync(Guid expenseId, AuditEntry audit, CancellationToken ct)
    {
        var expense = await _db.Expenses.FindAsync([expenseId], ct);
        if (expense is null)
        {
            _logger.LogDebug("Expense {ExpenseId} not found when persisting delete", expenseId);
            throw ValidationErrors.NotFound("Expense not found.");
        }

        _audit.Queue(audit);
        _db.Expenses.Remove(expense);

        await _db.SaveChangesAsync(ct);
    }
}
