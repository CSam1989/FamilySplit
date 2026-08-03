using FamilySplit.Common.Exceptions;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Common.Security;

/// <summary>
/// Caller-resolution guards shared by every slice that scopes data to the
/// caller's family or group membership. Scoped — shares the request's
/// <see cref="AppDbContext"/> unit-of-work.
/// </summary>
public sealed class GroupMembershipGuard : IGroupMembershipGuard
{
    private readonly AppDbContext _db;

    public GroupMembershipGuard(AppDbContext db) => _db = db;

    /// <summary>
    /// Resolves the caller's active FamilyId, or throws <see cref="ForbiddenException"/>
    /// when the user has no active FamilyMember.
    /// </summary>
    public async Task<Guid> GetCallerFamilyIdAsync(Guid callerId, CancellationToken ct)
    {
        return await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException();
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> unless the caller's family belongs
    /// to the group.
    /// </summary>
    public async Task RequireGroupMemberAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);

        var isMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId, ct);

        if (!isMember)
            throw new ForbiddenException();
    }
}
