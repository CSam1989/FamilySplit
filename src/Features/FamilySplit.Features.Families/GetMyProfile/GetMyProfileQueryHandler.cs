using FamilySplit.Features.Families.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.GetMyProfile;

/// <summary>
/// Query — the caller's own FamilyMember profile (absorbs the legacy <c>GET /users/me/profile</c>).
/// Pure data access: a single <c>AsNoTracking</c> projection mapped through
/// <see cref="FamilyMemberMapper"/>. A caller with no linked (active) FamilyMember returns null (404) —
/// the pre-existing wire-format the client depends on. Testcontainers-tested.
/// </summary>
public sealed class GetMyProfileQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<GetMyProfileQueryHandler> _logger;

    public GetMyProfileQueryHandler(AppDbContext db, ILogger<GetMyProfileQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FamilyMemberDto?> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("GetMyProfile called by {UserId}", callerId);

        var member = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == callerId && m.IsActive)
            .FirstOrDefaultAsync(ct);
        if (member is null)
            return null;

        return FamilyMemberMapper.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
