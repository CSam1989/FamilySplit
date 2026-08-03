using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.List;

/// <summary>
/// Query — lists the groups the caller's family belongs to. Pure data access: resolve the caller's
/// family (guard), then <c>AsNoTracking</c> projections. The invite code is exposed only to admin
/// families (it controls who can join).
/// </summary>
public sealed class ListGroupsQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<ListGroupsQueryHandler> _logger;

    public ListGroupsQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<ListGroupsQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<GroupSummaryDto>> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Listing groups for user {UserId}", callerId);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);

        // Groups the caller's family belongs to.
        var callerGroupFamilies = await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => gf.FamilyId == callerFamilyId)
            .Select(gf => new { gf.GroupId, gf.Role })
            .ToListAsync(ct);

        if (callerGroupFamilies.Count == 0) return [];

        var groupIds = callerGroupFamilies.Select(gf => gf.GroupId).ToList();

        var groups = await _db.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt })
            .ToListAsync(ct);

        // Family counts per group (one query).
        var familyCounts = await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => groupIds.Contains(gf.GroupId))
            .GroupBy(gf => gf.GroupId)
            .Select(grp => new { GroupId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, ct);

        var callerRoleByGroup = callerGroupFamilies.ToDictionary(gf => gf.GroupId, gf => gf.Role);

        return groups
            .Select(g =>
            {
                var role = callerRoleByGroup[g.Id];
                return new GroupSummaryDto(
                    g.Id,
                    g.Name,
                    g.Description,
                    // The invite code controls who can join — expose it only to admins.
                    role == MemberRole.Admin ? g.InviteCode : null,
                    familyCounts.GetValueOrDefault(g.Id, 0),
                    role,
                    g.CreatedAt);
            })
            .ToList();
    }
}
