using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.Data;

/// <summary>
/// The Families write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain values/records; writes
/// accept the command handler's computed state and persist it. Family mutations are non-financial, so
/// there is no audit flush. Verified with Testcontainers.
/// </summary>
internal sealed class FamilyData : IFamilyData
{
    private readonly AppDbContext _db;
    private readonly ILogger<FamilyData> _logger;

    public FamilyData(AppDbContext db, ILogger<FamilyData> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────

    public async Task<FamilyCallerMember?> GetCallerMemberAsync(Guid callerId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => new FamilyCallerMember(m.Id, m.FamilyId, m.IsAdmin))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> EmailInUseAsync(string email, Guid? excludeMemberId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .AnyAsync(m => m.IsActive && m.Email == email && (excludeMemberId == null || m.Id != excludeMemberId), ct);

    public async Task<FamilyMemberRecord?> GetActiveMemberInFamilyAsync(Guid memberId, Guid familyId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.Id == memberId && m.FamilyId == familyId && m.IsActive)
            .Select(m => new FamilyMemberRecord(m.Id, m.FamilyId, m.Email, m.IsAdmin))
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Email == email)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

    // ── writes ──────────────────────────────────────────────────────────────────

    public async Task UpdateFamilyNameAsync(Guid familyId, string name, CancellationToken ct)
    {
        var family = await _db.Families.FindAsync([familyId], ct);
        if (family is null)
        {
            _logger.LogDebug("Family {FamilyId} not found for rename", familyId);
            throw ValidationErrors.NotFound("Family not found.");
        }

        family.Name = name;
        family.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(FamilyMember member, CancellationToken ct)
    {
        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberAsync(Guid memberId, FamilyMemberFields fields, CancellationToken ct)
    {
        var member = await _db.FamilyMembers.FindAsync([memberId], ct);
        if (member is null)
        {
            _logger.LogDebug("FamilyMember {MemberId} not found for update", memberId);
            throw ValidationErrors.NotFound("Family member not found.");
        }

        member.DisplayName = fields.DisplayName;
        member.Email = fields.Email;
        member.DateOfBirth = fields.DateOfBirth;
        member.WeightOverride = fields.WeightOverride;
        member.IsAdmin = fields.IsAdmin;

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateMemberAsync(Guid memberId, CancellationToken ct)
    {
        var member = await _db.FamilyMembers.FindAsync([memberId], ct);
        if (member is null)
        {
            _logger.LogDebug("FamilyMember {MemberId} not found for deactivation", memberId);
            throw ValidationErrors.NotFound("Family member not found.");
        }

        member.IsActive = false;
        await _db.SaveChangesAsync(ct);
    }
}
