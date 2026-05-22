using FluentValidation;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families;
using FamilySplit.Application.Groups.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Application.Groups;

public class GroupService
{
    private readonly AppDbContext _db;
    private readonly CreateGroupValidator _createValidator;
    private readonly UpdateGroupValidator _updateValidator;
    private readonly JoinGroupValidator _joinValidator;

    public GroupService(
        AppDbContext db,
        CreateGroupValidator createValidator,
        UpdateGroupValidator updateValidator,
        JoinGroupValidator joinValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _joinValidator = joinValidator;
    }

    // ── List caller's groups ──────────────────────────────────────────────────

    public async Task<List<GroupSummaryDto>> ListAsync(Guid callerId)
    {
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);

        // Groups the caller's family belongs to.
        var callerGroupFamilies = await _db.GroupFamilies
            .Where(gf => gf.FamilyId == callerFamilyId)
            .Select(gf => new { gf.GroupId, gf.Role })
            .ToListAsync();

        if (callerGroupFamilies.Count == 0) return [];

        var groupIds = callerGroupFamilies.Select(gf => gf.GroupId).ToList();

        var groups = await _db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt })
            .ToListAsync();

        // Family counts per group (one query).
        var familyCounts = await _db.GroupFamilies
            .Where(gf => groupIds.Contains(gf.GroupId))
            .GroupBy(gf => gf.GroupId)
            .Select(grp => new { GroupId = grp.Key, Count = grp.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count);

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

    public async Task<GroupDetailDto> GetDetailAsync(Guid groupId, Guid callerId)
    {
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);
        var callerRole = await CallerFamilyRoleOrNullAsync(groupId, callerFamilyId)
            ?? throw Forbidden();

        return await BuildDetailDtoAsync(groupId, callerRole)
            ?? throw NotFound();
    }

    // ── Create group ──────────────────────────────────────────────────────────

    public async Task<GroupDetailDto> CreateAsync(CreateGroupRequest req, Guid callerId)
    {
        await _createValidator.ValidateAndThrowAsync(req);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);

        var group = new Group
        {
            Id              = Guid.NewGuid(),
            Name            = req.Name.Trim(),
            Description     = req.Description?.Trim(),
            InviteCode      = GenerateInviteCode(),
            CreatedByUserId = callerId,
            CreatedAt       = DateTimeOffset.UtcNow,
            UpdatedAt       = DateTimeOffset.UtcNow,
        };

        var adminGroupFamily = new GroupFamily
        {
            Id       = Guid.NewGuid(),
            GroupId  = group.Id,
            FamilyId = callerFamilyId,
            Role     = MemberRole.Admin,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        _db.Groups.Add(group);
        _db.GroupFamilies.Add(adminGroupFamily);
        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(group.Id, MemberRole.Admin)
            ?? throw NotFound();
    }

    // ── Update group ──────────────────────────────────────────────────────────

    public async Task<GroupDetailDto> UpdateAsync(Guid groupId, UpdateGroupRequest req, Guid callerId)
    {
        await _updateValidator.ValidateAndThrowAsync(req);
        await RequireAdminAsync(groupId, callerId);

        var group = await _db.Groups.FindAsync(groupId)
            ?? throw NotFound();

        group.Name        = req.Name.Trim();
        group.Description = req.Description?.Trim();
        group.UpdatedAt   = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(groupId, MemberRole.Admin)
            ?? throw NotFound();
    }

    // ── Join via invite code ──────────────────────────────────────────────────

    public async Task<GroupDetailDto> JoinAsync(JoinGroupRequest req, Guid callerId)
    {
        await _joinValidator.ValidateAndThrowAsync(req);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);

        var group = await _db.Groups
            .Where(g => g.InviteCode == req.InviteCode.ToUpperInvariant())
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync()
            ?? throw Throw422("InviteCode", "Invite code is invalid or has expired.");

        var alreadyMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == group.Id && gf.FamilyId == callerFamilyId);

        if (alreadyMember)
            throw Throw422("InviteCode", "Your family is already a member of this group.");

        var groupFamily = new GroupFamily
        {
            Id       = Guid.NewGuid(),
            GroupId  = group.Id,
            FamilyId = callerFamilyId,
            Role     = MemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        _db.GroupFamilies.Add(groupFamily);
        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(group.Id, MemberRole.Member)
            ?? throw NotFound();
    }

    // ── Regenerate invite code ────────────────────────────────────────────────

    public async Task<string> RegenerateInviteCodeAsync(Guid groupId, Guid callerId)
    {
        await RequireAdminAsync(groupId, callerId);

        var group = await _db.Groups.FindAsync(groupId)
            ?? throw NotFound();

        group.InviteCode = GenerateInviteCode();
        group.UpdatedAt  = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return group.InviteCode;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the FamilyId for the authenticated caller via their linked FamilyMember.
    /// Throws 403 if the user has no linked FamilyMember record.
    /// </summary>
    private async Task<Guid> GetCallerFamilyIdAsync(Guid userId)
    {
        var familyId = await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync();

        return familyId ?? throw Forbidden();
    }

    private async Task<MemberRole?> CallerFamilyRoleOrNullAsync(Guid groupId, Guid familyId)
    {
        var row = await _db.GroupFamilies
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .Select(gf => new { gf.Role })
            .FirstOrDefaultAsync();

        return row?.Role;
    }

    private async Task RequireAdminAsync(Guid groupId, Guid callerId)
    {
        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);
        var role = await CallerFamilyRoleOrNullAsync(groupId, callerFamilyId);
        if (role is null || role != MemberRole.Admin)
            throw Forbidden();
    }

    private async Task<GroupDetailDto?> BuildDetailDtoAsync(Guid groupId, MemberRole callerRole)
    {
        var group = await _db.Groups
            .Where(g => g.Id == groupId)
            .Select(g => new { g.Id, g.Name, g.Description, g.InviteCode, g.CreatedAt, g.UpdatedAt })
            .FirstOrDefaultAsync();

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
        ).ToListAsync();

        var familyIds = groupFamilyRows.Select(r => r.FamilyId).ToList();

        // Load all active FamilyMembers for those families in one query.
        var memberRows = await _db.FamilyMembers
            .Where(m => familyIds.Contains(m.FamilyId) && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();

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
