using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.ListFamilies;

/// <summary>
/// Query — lists every family with its active members (global-admin only). Pure data access: the
/// global-admin gate, then <c>AsNoTracking</c> reads mapped through <see cref="FamilyMemberMapper"/>.
/// Testcontainers-tested.
/// </summary>
public sealed class ListFamiliesQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ListFamiliesQueryHandler> _logger;

    public ListFamiliesQueryHandler(AppDbContext db, ILogger<ListFamiliesQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<FamilyDto>> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("ListFamilies called by {UserId}", callerId);

        await AdminGate.RequireGlobalAdminAsync(_db, callerId, _logger, ct);

        var families = await _db.Families
            .AsNoTracking()
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var familyIds = families.Select(f => f.Id).ToList();
        var allMembers = await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => familyIds.Contains(m.FamilyId) && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);

        var membersByFamily = allMembers
            .GroupBy(m => m.FamilyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return families.Select(f => new FamilyDto(
            f.Id,
            f.Name,
            membersByFamily.GetValueOrDefault(f.Id, [])
                .Select(m => FamilyMemberMapper.ToDto(m, today))
                .ToList(),
            f.CreatedAt,
            f.UpdatedAt)).ToList();
    }
}
