using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.ListForGroup;

/// <summary>
/// Query — active (non-Completed, non-Cancelled) settlements across all top-level activities of a
/// group the caller belongs to. Pure data access: membership guard then explicit-join projections.
/// Testcontainers-tested.
/// </summary>
public sealed class ListForGroupQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<ListForGroupQueryHandler> _logger;

    public ListForGroupQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<ListForGroupQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<GroupSettlementSummaryDto>> HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing settlements for group {GroupId} requested by user {UserId}", groupId, callerId);

        await _guard.RequireGroupMemberAsync(groupId, callerId, ct);

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.GroupId == groupId && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync(ct);

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();
        var nameMap = activities.ToDictionary(a => a.Id, a => a.Name);

        var rows = await (
            from s in _db.Settlements.AsNoTracking()
            join pf in _db.Families on s.PayerFamilyId equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where activityIds.Contains(s.ActivityId)
               && s.Status != SettlementStatus.Completed
               && s.Status != SettlementStatus.Cancelled
            orderby s.ProposedAt
            select new
            {
                s.Id,
                s.ActivityId,
                s.PayerFamilyId,
                PayerFamilyName = pf.Name,
                s.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                s.Amount,
                s.Currency,
                s.Status,
                s.ProposedAt,
            }
        ).ToListAsync(ct);

        _logger.LogDebug("Found {Count} pending settlements for group {GroupId}", rows.Count, groupId);

        return rows
            .Select(r => new GroupSettlementSummaryDto(
                r.Id,
                groupId,
                r.ActivityId,
                nameMap.GetValueOrDefault(r.ActivityId, "Unknown"),
                r.PayerFamilyId,
                r.PayerFamilyName,
                r.ReceiverFamilyId,
                r.ReceiverFamilyName,
                Math.Round(r.Amount, 2, MidpointRounding.AwayFromZero),
                r.Currency,
                r.Status,
                r.ProposedAt))
            .OrderBy(r => r.ActivityName)
            .ThenBy(r => r.ProposedAt)
            .ToList();
    }
}
