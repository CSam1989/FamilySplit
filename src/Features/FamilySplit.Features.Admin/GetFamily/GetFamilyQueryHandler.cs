using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.GetFamily;

/// <summary>
/// Query — one family with its active members (global-admin only). Pure data access: the global-admin
/// gate, then <c>AsNoTracking</c> reads mapped through <see cref="FamilyMemberMapper"/>. Missing family
/// → 422 (the wire shape the client expects). Testcontainers-tested.
/// </summary>
public sealed class GetFamilyQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetFamilyQueryHandler> _logger;

    public GetFamilyQueryHandler(AppDbContext db, ILogger<GetFamilyQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FamilyDto> HandleAsync(Guid familyId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("GetFamily called for {FamilyId} by {UserId}", familyId, callerId);

        await AdminGate.RequireGlobalAdminAsync(_db, callerId, ct);

        var family = await _db.Families
            .AsNoTracking()
            .Where(f => f.Id == familyId)
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.Field("FamilyId", "Family not found.");

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
