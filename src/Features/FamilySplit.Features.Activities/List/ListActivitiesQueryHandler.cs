using FamilySplit.Common.Security;
using FamilySplit.Features.Activities.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.List;

/// <summary>
/// Query — lists all top-level activities (no parent) for a group the caller belongs to. Pure data
/// access: membership guard, then <c>AsNoTracking</c> projections with the participant / sub-activity
/// counts and expense aggregates computed in the database. Testcontainers-tested.
/// </summary>
public sealed class ListActivitiesQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<ListActivitiesQueryHandler> _logger;

    public ListActivitiesQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<ListActivitiesQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<ActivitySummaryDto>> HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing activities for group {GroupId} by user {UserId}", groupId, callerId);

        await _guard.RequireGroupMemberAsync(groupId, callerId, ct);

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.GroupId == groupId && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();

        // Aggregate participant counts, sub-activity counts, and expense totals
        // entirely in the database — avoids pulling every Expense row to the API.
        var participantCounts = await _db.ActivityParticipants
            .AsNoTracking()
            .Where(ap => activityIds.Contains(ap.ActivityId))
            .GroupBy(ap => ap.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count, ct);

        var subActivityCounts = await _db.Activities
            .AsNoTracking()
            .Where(a => a.ParentActivityId != null && activityIds.Contains(a.ParentActivityId.Value))
            .GroupBy(a => a.ParentActivityId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count, ct);

        // Sum + count per activity in one query; currency picked via a min() so the
        // database doesn't have to ship per-row data. In practice all expenses on
        // an activity share a currency.
        var expenseAggregates = await _db.Expenses
            .AsNoTracking()
            .Where(e => activityIds.Contains(e.ActivityId))
            .GroupBy(e => e.ActivityId)
            .Select(g => new
            {
                ActivityId = g.Key,
                Count = g.Count(),
                Total = g.Sum(e => e.TotalAmount),
                Currency = g.Min(e => e.Currency)!,
            })
            .ToDictionaryAsync(x => x.ActivityId, ct);

        return activities.Select(a =>
        {
            expenseAggregates.TryGetValue(a.Id, out var agg);
            return new ActivitySummaryDto(
                a.Id,
                a.GroupId,
                a.Name,
                a.Description,
                a.Status,
                a.ParentActivityId,
                participantCounts.GetValueOrDefault(a.Id, 0),
                subActivityCounts.GetValueOrDefault(a.Id, 0),
                a.CreatedAt,
                a.ClosedAt,
                agg?.Count ?? 0,
                agg?.Total ?? 0m,
                agg?.Currency ?? "EUR");
        }).ToList();
    }
}
