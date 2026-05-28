using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families;
using FamilySplit.Application.Groups.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Groups;

public class GroupService
{
    private readonly AppDbContext _db;
    private readonly CreateGroupValidator _createValidator;
    private readonly UpdateGroupValidator _updateValidator;
    private readonly JoinGroupValidator _joinValidator;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        AppDbContext db,
        CreateGroupValidator createValidator,
        UpdateGroupValidator updateValidator,
        JoinGroupValidator joinValidator,
        ILogger<GroupService> logger)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _joinValidator = joinValidator;
        _logger = logger;
    }

    // ── List caller's groups ──────────────────────────────────────────────────

    public async Task<List<GroupSummaryDto>> ListAsync(Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("ListAsync called. {UserId}", callerId);
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);

        // Groups the caller's family belongs to.
        var callerGroupFamilies = await _db.GroupFamilies
            .Where(gf => gf.FamilyId == callerFamilyId)
            .Select(gf => new { gf.GroupId, gf.Role })
            .ToListAsync(ct);

        if (callerGroupFamilies.Count == 0) return [];

        var groupIds = callerGroupFamilies.Select(gf => gf.GroupId).ToList();

        var groups = await _db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt })
            .ToListAsync(ct);

        // Family counts per group (one query).
        var familyCounts = await _db.GroupFamilies
            .Where(gf => groupIds.Contains(gf.GroupId))
            .GroupBy(gf => gf.GroupId)
            .Select(grp => new { GroupId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, ct);

        var callerRoleByGroup = callerGroupFamilies.ToDictionary(gf => gf.GroupId, gf => gf.Role);

        return groups
            .Select(g => new GroupSummaryDto(
                g.Id,
                g.Name,
                g.Description,
                g.InviteCode,
                familyCounts.GetValueOrDefault(g.Id, 0),
                callerRoleByGroup[g.Id],
                g.CreatedAt))
            .ToList();
    }

    // ── Get group detail ──────────────────────────────────────────────────────

    public async Task<GroupDetailDto> GetDetailAsync(Guid groupId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetDetailAsync called. {GroupId} {UserId}", groupId, callerId);
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);
        var callerRole = await CallerFamilyRoleOrNullAsync(groupId, callerFamilyId, ct)
            ?? throw Forbidden();

        return await BuildDetailDtoAsync(groupId, callerRole, ct)
            ?? throw NotFound();
    }

    // ── Create group ──────────────────────────────────────────────────────────

    public async Task<GroupDetailDto> CreateAsync(CreateGroupRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("CreateAsync called. {UserId} Name={Name}", callerId, req.Name);
        await _createValidator.ValidateAndThrowAsync(req, ct);
        await RequireCallerIsFamilyAdminAsync(callerId, ct);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            InviteCode = GenerateInviteCode(),
            CreatedByUserId = callerId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var adminGroupFamily = new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            FamilyId = callerFamilyId,
            Role = MemberRole.Admin,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        _db.Groups.Add(group);
        _db.GroupFamilies.Add(adminGroupFamily);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Group created. {GroupId} by {UserId}", group.Id, callerId);

        return await BuildDetailDtoAsync(group.Id, MemberRole.Admin, ct)
            ?? throw NotFound();
    }

    // ── Update group ──────────────────────────────────────────────────────────

    public async Task<GroupDetailDto> UpdateAsync(Guid groupId, UpdateGroupRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("UpdateAsync called. {GroupId} {UserId}", groupId, callerId);
        await _updateValidator.ValidateAndThrowAsync(req, ct);
        await RequireAdminAsync(groupId, callerId, ct);

        var group = await _db.Groups.FindAsync([groupId], ct)
            ?? throw NotFound();

        group.Name = req.Name.Trim();
        group.Description = req.Description?.Trim();
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Group updated. {GroupId} by {UserId}", groupId, callerId);

        return await BuildDetailDtoAsync(groupId, MemberRole.Admin, ct)
            ?? throw NotFound();
    }

    // ── Join via invite code ──────────────────────────────────────────────────

    public async Task<GroupDetailDto> JoinAsync(JoinGroupRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("JoinAsync called. {UserId}", callerId);
        await _joinValidator.ValidateAndThrowAsync(req, ct);
        await RequireCallerIsFamilyAdminAsync(callerId, ct);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);

        var group = await _db.Groups
            .Where(g => g.InviteCode == req.InviteCode.ToUpperInvariant())
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("InviteCode", "Invite code is invalid or has expired.");

        var alreadyMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == group.Id && gf.FamilyId == callerFamilyId, ct);

        if (alreadyMember)
            throw Throw422("InviteCode", "Your family is already a member of this group.");

        var groupFamily = new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            FamilyId = callerFamilyId,
            Role = MemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        _db.GroupFamilies.Add(groupFamily);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Family joined group. {GroupId} {FamilyId} by {UserId}", group.Id, callerFamilyId, callerId);

        return await BuildDetailDtoAsync(group.Id, MemberRole.Member, ct)
            ?? throw NotFound();
    }

    // ── Leave group ───────────────────────────────────────────────────────────

    public async Task LeaveAsync(Guid groupId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("LeaveAsync called. {GroupId} {UserId}", groupId, callerId);
        await RequireCallerIsFamilyAdminAsync(callerId, ct);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);

        var groupFamily = await _db.GroupFamilies
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId)
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("Group", "Your family is not a member of this group.");

        // Guard: if this family is the sole admin, refuse the leave.
        if (groupFamily.Role == MemberRole.Admin)
        {
            var adminCount = await _db.GroupFamilies
                .CountAsync(gf => gf.GroupId == groupId && gf.Role == MemberRole.Admin, ct);

            if (adminCount <= 1)
                throw Throw422("Group",
                    "Cannot leave: your family is the only admin of this group. " +
                    "Transfer the admin role to another family first.");
        }

        _db.GroupFamilies.Remove(groupFamily);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Family left group. {GroupId} {FamilyId} by {UserId}", groupId, callerFamilyId, callerId);
    }

    // ── Regenerate invite code ────────────────────────────────────────────────

    public async Task<string> RegenerateInviteCodeAsync(Guid groupId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("RegenerateInviteCodeAsync called. {GroupId} {UserId}", groupId, callerId);
        await RequireAdminAsync(groupId, callerId, ct);

        var group = await _db.Groups.FindAsync([groupId], ct)
            ?? throw NotFound();

        group.InviteCode = GenerateInviteCode();
        group.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Invite code regenerated (old code is now invalid). {GroupId} by {UserId}", groupId, callerId);

        return group.InviteCode;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Throws 403 unless the caller's active FamilyMember has IsAdmin = true.
    /// </summary>
    private async Task RequireCallerIsFamilyAdminAsync(Guid userId, CancellationToken ct)
    {
        var isAdmin = await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => (bool?)m.IsAdmin)
            .FirstOrDefaultAsync(ct);

        if (isAdmin is not true)
            throw Forbidden();
    }

    /// <summary>
    /// Resolves the FamilyId for the authenticated caller via their linked FamilyMember.
    /// Throws 403 if the user has no linked FamilyMember record.
    /// </summary>
    private async Task<Guid> GetCallerFamilyIdAsync(Guid userId, CancellationToken ct)
    {
        var familyId = await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        return familyId ?? throw Forbidden();
    }

    private async Task<MemberRole?> CallerFamilyRoleOrNullAsync(Guid groupId, Guid familyId, CancellationToken ct)
    {
        var row = await _db.GroupFamilies
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .Select(gf => new { gf.Role })
            .FirstOrDefaultAsync(ct);

        return row?.Role;
    }

    private async Task RequireAdminAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId, ct);
        var role = await CallerFamilyRoleOrNullAsync(groupId, callerFamilyId, ct);
        if (role is null || role != MemberRole.Admin)
            throw Forbidden();
    }

    private async Task<GroupDetailDto?> BuildDetailDtoAsync(Guid groupId, MemberRole callerRole, CancellationToken ct)
    {
        var group = await _db.Groups
            .Where(g => g.Id == groupId)
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt, g.UpdatedAt })
            .FirstOrDefaultAsync(ct);

        if (group is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Load all GroupFamily rows for this group with their Family names.
        // Explicit join to avoid EF Core navigation cycle detection.
        var groupFamilyRows = await (
            from gf in _db.GroupFamilies
            join f in _db.Families on gf.FamilyId equals f.Id
            where gf.GroupId == groupId
            orderby gf.JoinedAt
            select new { gf.FamilyId, f.Name, gf.Role, gf.JoinedAt }
        ).ToListAsync(ct);

        var familyIds = groupFamilyRows.Select(r => r.FamilyId).ToList();

        // Load all active FamilyMembers for those families in one query.
        var memberRows = await _db.FamilyMembers
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
            group.InviteCode,
            callerRole,
            families,
            group.CreatedAt,
            group.UpdatedAt);
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I
        return new string(Enumerable.Range(0, 8)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }

    private static ForbiddenException Forbidden() => new();
    private static ValidationException NotFound() => new("Group not found.");
    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
