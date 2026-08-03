using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.List;

/// <summary>
/// Query — lists the expenses on an activity as summary DTOs. Pure data access:
/// authorization guard then <c>AsNoTracking</c> projections, no mutation.
/// </summary>
public sealed class ListExpensesQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<ListExpensesQueryHandler> _logger;

    public ListExpensesQueryHandler(
        AppDbContext db,
        GroupMembershipGuard guard,
        ILogger<ListExpensesQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<ExpenseSummaryDto>> HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing expenses for activity {ActivityId} requested by user {UserId}",
            activityId, callerId);

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var expenses = await _db.Expenses
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(ep => expenseIds.Contains(ep.ExpenseId) && !ep.IsExcluded)
            .GroupBy(ep => ep.ExpenseId)
            .Select(g => new { ExpenseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExpenseId, x => x.Count, ct);

        // Payer info (User → FamilyMember → Family) for each unique payer.
        var payerUserIds = expenses.Select(e => e.PaidByUserId).Distinct().ToList();
        var payerInfo = await (
            from fm in _db.FamilyMembers.AsNoTracking()
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
}
