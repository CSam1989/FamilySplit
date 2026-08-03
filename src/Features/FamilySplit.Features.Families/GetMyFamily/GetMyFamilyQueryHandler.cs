using FamilySplit.Features.Families.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.GetMyFamily;

/// <summary>
/// Query — the caller's own Family with all active members. Pure data access: <c>AsNoTracking</c>
/// reads mapped through <see cref="FamilyMemberMapper"/>. A caller with no linked (active) FamilyMember
/// returns null (404) — this is the pre-existing wire-format the client depends on, not a 403; it is
/// intentionally not routed through the throwing <c>IGroupMembershipGuard</c>. Testcontainers-tested.
/// </summary>
public sealed class GetMyFamilyQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetMyFamilyQueryHandler> _logger;

    public GetMyFamilyQueryHandler(AppDbContext db, ILogger<GetMyFamilyQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FamilyDto?> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("GetMyFamily called by {UserId}", callerId);

        var familyId = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);
        if (familyId is null)
            return null;

        var family = await _db.Families
            .AsNoTracking()
            .Where(f => f.Id == familyId)
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .FirstOrDefaultAsync(ct);
        if (family is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var members = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);

        return new FamilyDto(
            family.Id,
            family.Name,
            members.Select(m => FamilyMemberMapper.ToDto(m, today)).ToList(),
            family.CreatedAt,
            family.UpdatedAt);
    }
}
