using FamilySplit.Common.Calculations;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.GetDetail;

/// <summary>
/// Query — full detail for a group the caller's family belongs to (absorbs the old service's
/// <c>BuildDetailDtoAsync</c>). Pure data access: caller-family resolution + membership guard, then
/// explicit-join projections (no navigation properties, per the EF Core 10 cycle-detection
/// workaround). The invite code is exposed only to admin families.
/// </summary>
public sealed class GetGroupDetailQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<GetGroupDetailQueryHandler> _logger;

    public GetGroupDetailQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<GetGroupDetailQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<GroupDetailDto> HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Fetching group {GroupId} for user {UserId}", groupId, callerId);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);

        var callerRole = await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId)
            .Select(gf => (MemberRole?)gf.Role)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException();

        var group = await _db.Groups
            .AsNoTracking()
            .Where(g => g.Id == groupId)
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt, g.UpdatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Group not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // All GroupFamily rows for this group with their Family names.
        // Explicit join to avoid EF Core navigation cycle detection.
        var groupFamilyRows = await (
            from gf in _db.GroupFamilies.AsNoTracking()
            join f in _db.Families on gf.FamilyId equals f.Id
            where gf.GroupId == groupId
            orderby gf.JoinedAt
            select new { gf.FamilyId, f.Name, gf.Role, gf.JoinedAt }
        ).ToListAsync(ct);

        var familyIds = groupFamilyRows.Select(r => r.FamilyId).ToList();

        // All active FamilyMembers for those families in one query.
        var memberRows = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => familyIds.Contains(m.FamilyId) && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);

        var membersByFamily = memberRows
            .GroupBy(m => m.FamilyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var families = groupFamilyRows.Select(gf =>
        {
            var members = membersByFamily.GetValueOrDefault(gf.FamilyId, [])
                .Select(m => new GroupMemberSummaryDto(
                    m.Id,
                    m.DisplayName,
                    WeightCalculator.GetWeight(m, today),
                    WeightCalculator.GetTier(m, today),
                    m.UserId is not null))
                .ToList();

            return new GroupFamilyDto(gf.FamilyId, gf.Name, gf.Role, gf.JoinedAt, members);
        }).ToList();

        return new GroupDetailDto(
            group.Id,
            group.Name,
            group.Description,
            // The invite code controls who can join — expose it only to admins.
            callerRole == MemberRole.Admin ? group.InviteCode : null,
            callerRole,
            families,
            group.CreatedAt,
            group.UpdatedAt);
    }
}
