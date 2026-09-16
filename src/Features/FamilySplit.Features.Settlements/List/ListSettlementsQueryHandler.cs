using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.List;

/// <summary>
/// Query — lists the settlements for an activity the caller's family belongs to. Pure data access:
/// lookup + membership guard, then an explicit-join projection (no navigation properties, per the
/// EF Core 10 cycle-detection workaround). Testcontainers-tested.
/// </summary>
public sealed class ListSettlementsQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<ListSettlementsQueryHandler> _logger;

    public ListSettlementsQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<ListSettlementsQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<SettlementSummaryDto>> HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing settlements for activity {ActivityId} requested by user {UserId}", activityId, callerId);

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            _logger.LogDebug("Activity {ActivityId} not found for settlement list requested by user {UserId}", activityId, callerId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var rows = await (
            from s in _db.Settlements.AsNoTracking()
            join pf in _db.Families on s.PayerFamilyId equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where s.ActivityId == activityId
            orderby s.ProposedAt
            select new SettlementSummaryDto(
                s.Id,
                s.ActivityId,
                s.PayerFamilyId,
                pf.Name,
                s.ReceiverFamilyId,
                rf.Name,
                s.Amount,
                s.Currency,
                s.Status,
                s.ProposedAt,
                s.CompletedAt)
        ).ToListAsync(ct);

        return rows;
    }
}
