using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Features.Groups.Data;

/// <summary>
/// The Groups write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain values/records;
/// writes accept the command handler's computed state and persist it. Verified with Testcontainers
/// through the group endpoints.
/// </summary>
internal sealed class GroupData : IGroupData
{
    private readonly AppDbContext _db;

    public GroupData(AppDbContext db) => _db = db;

    public async Task<bool> IsActiveFamilyAdminAsync(Guid callerId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (bool?)m.IsAdmin)
            .FirstOrDefaultAsync(ct) is true;

    public async Task<MemberRole?> GetFamilyRoleInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct) =>
        await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .Select(gf => (MemberRole?)gf.Role)
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> GetGroupIdByInviteCodeAsync(string inviteCode, CancellationToken ct) =>
        await _db.Groups
            .AsNoTracking()
            .Where(g => g.InviteCode == inviteCode)
            .Select(g => (Guid?)g.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> IsFamilyInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct) =>
        await _db.GroupFamilies
            .AsNoTracking()
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == familyId, ct);

    public async Task<GroupMembershipInfo?> GetFamilyMembershipAsync(Guid groupId, Guid familyId, CancellationToken ct) =>
        await _db.GroupFamilies
            .AsNoTracking()
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .Select(gf => new GroupMembershipInfo(gf.Id, gf.Role))
            .FirstOrDefaultAsync(ct);

    public async Task<int> CountGroupAdminsAsync(Guid groupId, CancellationToken ct) =>
        await _db.GroupFamilies
            .AsNoTracking()
            .CountAsync(gf => gf.GroupId == groupId && gf.Role == MemberRole.Admin, ct);

    public async Task<string> GenerateUniqueInviteCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = InviteCodeGenerator.NewCode();
            var taken = await _db.Groups.AsNoTracking().AnyAsync(g => g.InviteCode == code, ct);
            if (!taken)
                return code;
        }

        throw new InvalidOperationException("Unable to generate a unique invite code after several attempts.");
    }

    public async Task AddGroupAsync(Group group, GroupFamily adminMembership, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        group.CreatedAt = now;
        group.UpdatedAt = now;
        adminMembership.JoinedAt = now;

        _db.Groups.Add(group);
        _db.GroupFamilies.Add(adminMembership);

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateGroupDetailsAsync(Guid groupId, string name, string? description, CancellationToken ct)
    {
        var group = await _db.Groups.FindAsync([groupId], ct)
            ?? throw ValidationErrors.NotFound("Group not found.");

        group.Name = name;
        group.Description = description;
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task AddFamilyToGroupAsync(GroupFamily membership, CancellationToken ct)
    {
        membership.JoinedAt = DateTimeOffset.UtcNow;
        _db.GroupFamilies.Add(membership);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveFamilyFromGroupAsync(Guid groupFamilyId, CancellationToken ct)
    {
        var membership = await _db.GroupFamilies.FindAsync([groupFamilyId], ct);
        if (membership is null)
            return;

        _db.GroupFamilies.Remove(membership);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateInviteCodeAsync(Guid groupId, string newCode, CancellationToken ct)
    {
        var group = await _db.Groups.FindAsync([groupId], ct)
            ?? throw ValidationErrors.NotFound("Group not found.");

        group.InviteCode = newCode;
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
