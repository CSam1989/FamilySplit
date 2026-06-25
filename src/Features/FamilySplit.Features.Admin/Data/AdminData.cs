using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Features.Admin.Data;

/// <summary>
/// The Admin write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain values/records; writes
/// accept the command handler's computed state and persist it. Admin mutations are non-financial, so
/// there is no audit flush. Verified with Testcontainers.
/// </summary>
internal sealed class AdminData : IAdminData
{
    private readonly AppDbContext _db;

    public AdminData(AppDbContext db) => _db = db;

    // ── reads ─────────────────────────────────────────────────────────────────────

    public async Task<bool> IsGlobalAdminAsync(Guid callerId, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> FamilyExistsAsync(Guid familyId, CancellationToken ct) =>
        await _db.Families.AsNoTracking().AnyAsync(f => f.Id == familyId, ct);

    public async Task<bool> GroupExistsAsync(Guid groupId, CancellationToken ct) =>
        await _db.Groups.AsNoTracking().AnyAsync(g => g.Id == groupId, ct);

    public async Task<bool> EmailInUseAsync(string email, Guid? excludeMemberId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .AnyAsync(m => m.IsActive && m.Email == email && (excludeMemberId == null || m.Id != excludeMemberId), ct);

    public async Task<AdminMemberRecord?> GetActiveMemberAsync(Guid memberId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.Id == memberId && m.IsActive)
            .Select(m => new AdminMemberRecord(m.Id, m.FamilyId, m.Email))
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> FamilyInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct) =>
        await _db.GroupFamilies
            .AsNoTracking()
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == familyId, ct);

    // ── writes ──────────────────────────────────────────────────────────────────

    public async Task AddFamilyAsync(Family family, CancellationToken ct)
    {
        _db.Families.Add(family);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(FamilyMember member, CancellationToken ct)
    {
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberAsync(Guid memberId, AdminMemberFields fields, CancellationToken ct)
    {
        var member = await _db.FamilyMembers.FindAsync([memberId], ct)
            ?? throw ValidationErrors.NotFound("Family member not found.");

        member.DisplayName = fields.DisplayName;
        member.Email = fields.Email;
        member.DateOfBirth = fields.DateOfBirth;
        member.WeightOverride = fields.WeightOverride;
        member.IsAdmin = fields.IsAdmin;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateMemberAsync(Guid memberId, CancellationToken ct)
    {
        var member = await _db.FamilyMembers.FindAsync([memberId], ct)
            ?? throw ValidationErrors.NotFound("Family member not found.");

        member.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddFamilyToGroupAsync(GroupFamily membership, CancellationToken ct)
    {
        _db.GroupFamilies.Add(membership);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveFamilyFromGroupAsync(Guid groupId, Guid familyId, CancellationToken ct)
    {
        var membership = await _db.GroupFamilies
            .FirstOrDefaultAsync(gf => gf.GroupId == groupId && gf.FamilyId == familyId, ct);
        if (membership is null)
            return;

        _db.GroupFamilies.Remove(membership);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct)
    {
        // Activity.ParentActivityId is a RESTRICT self-FK, so the group→activities cascade can fail
        // if a parent is deleted before its sub-activities. Delete sub-activities first, then the
        // group (whose cascade removes the parents). Both run in one transaction via the retrying
        // execution strategy.
        var strategy = _db.Database.CreateExecutionStrategy();
        var deleted = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            await _db.Activities
                .Where(a => a.GroupId == groupId && a.ParentActivityId != null)
                .ExecuteDeleteAsync(ct);

            var rows = await _db.Groups
                .Where(g => g.Id == groupId)
                .ExecuteDeleteAsync(ct);

            await tx.CommitAsync(ct);
            return rows;
        });

        return deleted > 0;
    }
}
