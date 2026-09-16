using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.ListMyPending;

/// <summary>
/// Query — every active settlement involving the caller's family across all their groups (the
/// dashboard view). Pure data access: resolve the caller's family inline (the only place that
/// throws <see cref="ForbiddenException"/> directly), then explicit-join projections.
/// Testcontainers-tested.
/// </summary>
public sealed class ListMyPendingQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ListMyPendingQueryHandler> _logger;

    public ListMyPendingQueryHandler(AppDbContext db, ILogger<ListMyPendingQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<GroupSettlementSummaryDto>> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing all pending settlements for user {UserId}", callerId);

        var callerFamilyId = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);
        if (callerFamilyId is null)
        {
            _logger.LogWarning("User {UserId} with no active FamilyMember attempted to list pending settlements", callerId);
            throw new ForbiddenException();
        }

        var groupIds = await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => gf.FamilyId == callerFamilyId)
            .Select(gf => gf.GroupId)
            .ToListAsync(ct);

        if (groupIds.Count == 0) return [];

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => groupIds.Contains(a.GroupId) && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.GroupId, a.Name })
            .ToListAsync(ct);

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();
        var activityMap = activities.ToDictionary(a => a.Id, a => new { a.GroupId, a.Name });

        var rows = await (
            from s in _db.Settlements.AsNoTracking()
            join pf in _db.Families on s.PayerFamilyId equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where activityIds.Contains(s.ActivityId)
               && s.Status != SettlementStatus.Completed
               && s.Status != SettlementStatus.Cancelled
               && (s.PayerFamilyId == callerFamilyId || s.ReceiverFamilyId == callerFamilyId)
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

        _logger.LogDebug("Found {Count} pending settlements for user {UserId} across {GroupCount} group(s)",
            rows.Count, callerId, groupIds.Count);

        return rows
            .Select(r =>
            {
                var act = activityMap[r.ActivityId];
                return new GroupSettlementSummaryDto(
                    r.Id,
                    act.GroupId,
                    r.ActivityId,
                    act.Name,
                    r.PayerFamilyId,
                    r.PayerFamilyName,
                    r.ReceiverFamilyId,
                    r.ReceiverFamilyName,
                    Math.Round(r.Amount, 2, MidpointRounding.AwayFromZero),
                    r.Currency,
                    r.Status,
                    r.ProposedAt);
            })
            .OrderBy(r => r.ActivityName)
            .ThenBy(r => r.ProposedAt)
            .ToList();
    }
}
