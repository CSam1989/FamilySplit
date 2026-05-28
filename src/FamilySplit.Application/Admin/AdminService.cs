using FluentValidation;
using FamilySplit.Application.Admin.Dtos;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families;
using FamilySplit.Application.Families.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Admin;

/// <summary>
/// Global-admin operations: create and manage Families and their members.
/// Every method requires the caller to have <c>User.IsGlobalAdmin = true</c>.
/// </summary>
public class AdminService
{
    private readonly AppDbContext _db;
    private readonly CreateFamilyValidator _createFamilyValidator;
    private readonly AddFamilyMemberValidator _addMemberValidator;
    private readonly UpdateFamilyMemberValidator _updateMemberValidator;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        AppDbContext db,
        CreateFamilyValidator createFamilyValidator,
        AddFamilyMemberValidator addMemberValidator,
        UpdateFamilyMemberValidator updateMemberValidator,
        ILogger<AdminService> logger)
    {
        _db = db;
        _createFamilyValidator = createFamilyValidator;
        _addMemberValidator = addMemberValidator;
        _updateMemberValidator = updateMemberValidator;
        _logger = logger;
    }

    // ── List all families ─────────────────────────────────────────────────────

    public async Task<List<FamilyDto>> ListFamiliesAsync(Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("ListFamiliesAsync called by {UserId}", callerId);
        await RequireGlobalAdminAsync(callerId, ct);

        var families = await _db.Families
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Load all members in one query, group in memory.
        var familyIds = families.Select(f => f.Id).ToList();
        var allMembers = await _db.FamilyMembers
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
                .Select(m => FamilyService.ToDto(m, today))
                .ToList(),
            f.CreatedAt,
            f.UpdatedAt)).ToList();
    }

    // ── Get one family ────────────────────────────────────────────────────────

    public async Task<FamilyDto> GetFamilyAsync(Guid familyId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetFamilyAsync called for {FamilyId} by {UserId}", familyId, callerId);
        await RequireGlobalAdminAsync(callerId, ct);
        return await BuildFamilyDtoAsync(familyId, ct)
            ?? throw Throw422("FamilyId", "Family not found.");
    }

    // ── Create family ─────────────────────────────────────────────────────────

    public async Task<FamilyDto> CreateFamilyAsync(CreateFamilyRequest req, Guid callerId, CancellationToken ct = default)
    {
        await _createFamilyValidator.ValidateAndThrowAsync(req, ct);
        await RequireGlobalAdminAsync(callerId, ct);

        var family = new Family
        {
            Id               = Guid.NewGuid(),
            Name             = req.Name.Trim(),
            CreatedByUserId  = callerId,
            CreatedAt        = DateTimeOffset.UtcNow,
            UpdatedAt        = DateTimeOffset.UtcNow
        };

        _db.Families.Add(family);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Family {FamilyId} created by global admin {UserId}", family.Id, callerId);

        return await BuildFamilyDtoAsync(family.Id, ct)
            ?? throw new InvalidOperationException("Family not found after create.");
    }

    // ── Add member to a family ────────────────────────────────────────────────

    public async Task<FamilyMemberDto> AddFamilyMemberAsync(
        Guid familyId,
        AddFamilyMemberRequest req,
        Guid callerId,
        CancellationToken ct = default)
    {
        await _addMemberValidator.ValidateAndThrowAsync(req, ct);
        await RequireGlobalAdminAsync(callerId, ct);

        var familyExists = await _db.Families.AnyAsync(f => f.Id == familyId, ct);
        if (!familyExists)
            throw Throw422("FamilyId", "Family not found.");

        var emailNorm = req.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null)
        {
            // Direct equality — email column stores canonical lowercase form.
            var conflict = await _db.FamilyMembers
                .AnyAsync(m => m.IsActive && m.Email == emailNorm, ct);
            if (conflict)
                throw Throw422("Email", "A family member with this email already exists.");
        }

        var member = new FamilyMember
        {
            Id             = Guid.NewGuid(),
            FamilyId       = familyId,
            Email          = emailNorm,
            UserId         = null,
            IsAdmin        = req.IsAdmin,
            DisplayName    = req.DisplayName.Trim(),
            DateOfBirth    = req.DateOfBirth,
            WeightOverride = req.WeightOverride,
            IsActive       = true,
            CreatedAt      = DateTimeOffset.UtcNow
        };

        // Auto-link if a User with this email already exists. User.Email is
        // stored lowercased (normalized in OAuthHandler).
        if (emailNorm is not null)
        {
            var existingUser = await _db.Users
                .Where(u => u.Email == emailNorm)
                .Select(u => new { u.Id })
                .FirstOrDefaultAsync(ct);

            member.UserId = existingUser?.Id;
        }

        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "FamilyMember {MemberId} added to family {FamilyId} by global admin {UserId} (linked: {IsLinked})",
            member.Id, familyId, callerId, member.UserId is not null);

        return FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Update any member ─────────────────────────────────────────────────────

    public async Task<FamilyMemberDto> UpdateFamilyMemberAsync(
        Guid memberId,
        UpdateFamilyMemberRequest req,
        Guid callerId,
        CancellationToken ct = default)
    {
        await _updateMemberValidator.ValidateAndThrowAsync(req, ct);
        await RequireGlobalAdminAsync(callerId, ct);

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.IsActive)
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("MemberId", "Family member not found.");

        var emailNorm = req.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && emailNorm != member.Email)
        {
            var conflict = await _db.FamilyMembers
                .AnyAsync(m => m.IsActive && m.Id != memberId && m.Email == emailNorm, ct);
            if (conflict)
                throw Throw422("Email", "A family member with this email already exists.");
        }

        member.DisplayName    = req.DisplayName.Trim();
        member.Email          = emailNorm;
        member.DateOfBirth    = req.DateOfBirth;
        member.WeightOverride = req.WeightOverride;
        member.IsAdmin        = req.IsAdmin;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {MemberId} updated by global admin {UserId}", memberId, callerId);

        return FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Remove any member ─────────────────────────────────────────────────────

    public async Task RemoveFamilyMemberAsync(Guid memberId, Guid callerId, CancellationToken ct = default)
    {
        await RequireGlobalAdminAsync(callerId, ct);

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.IsActive)
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("MemberId", "Family member not found.");

        member.IsActive = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {MemberId} deactivated by global admin {UserId}", memberId, callerId);
    }

    // ── Add a family to a group ───────────────────────────────────────────────

    public async Task AddFamilyToGroupAsync(Guid groupId, Guid familyId, Guid callerId, CancellationToken ct = default)
    {
        await RequireGlobalAdminAsync(callerId, ct);

        var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupId, ct);
        if (!groupExists)
            throw Throw422("GroupId", "Group not found.");

        var familyExists = await _db.Families.AnyAsync(f => f.Id == familyId, ct);
        if (!familyExists)
            throw Throw422("FamilyId", "Family not found.");

        var alreadyMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == familyId, ct);
        if (alreadyMember)
            throw Throw422("FamilyId", "This family is already in the group.");

        _db.GroupFamilies.Add(new GroupFamily
        {
            Id       = Guid.NewGuid(),
            GroupId  = groupId,
            FamilyId = familyId,
            Role     = MemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Family {FamilyId} added to group {GroupId} by global admin {UserId}", familyId, groupId, callerId);
    }

    // ── Remove a family from a group ──────────────────────────────────────────

    public async Task RemoveFamilyFromGroupAsync(Guid groupId, Guid familyId, Guid callerId, CancellationToken ct = default)
    {
        await RequireGlobalAdminAsync(callerId, ct);

        var groupFamily = await _db.GroupFamilies
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("FamilyId", "This family is not in the group.");

        _db.GroupFamilies.Remove(groupFamily);
        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Family {FamilyId} removed from group {GroupId} by global admin {UserId}", familyId, groupId, callerId);
    }

    // ── Delete a group ────────────────────────────────────────────────────────

    public async Task DeleteGroupAsync(Guid groupId, Guid callerId, CancellationToken ct = default)
    {
        await RequireGlobalAdminAsync(callerId, ct);

        var deleted = await _db.Groups
            .Where(g => g.Id == groupId)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
            throw Throw422("GroupId", "Group not found.");

        _logger.LogWarning("Group {GroupId} deleted by global admin {UserId}", groupId, callerId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireGlobalAdminAsync(Guid userId, CancellationToken ct)
    {
        var isAdmin = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync(ct);

        if (!isAdmin) throw Forbidden();
    }

    private async Task<FamilyDto?> BuildFamilyDtoAsync(Guid familyId, CancellationToken ct)
    {
        var family = await _db.Families
            .Where(f => f.Id == familyId)
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .FirstOrDefaultAsync(ct);

        if (family is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var members = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);

        return new FamilyDto(
            family.Id,
            family.Name,
            members.Select(m => FamilyService.ToDto(m, today)).ToList(),
            family.CreatedAt,
            family.UpdatedAt);
    }

    private static ForbiddenException Forbidden() => new();
    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
