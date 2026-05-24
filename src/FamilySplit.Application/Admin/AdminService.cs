using FluentValidation;
using FamilySplit.Application.Admin.Dtos;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families;
using FamilySplit.Application.Families.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

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

    public AdminService(
        AppDbContext db,
        CreateFamilyValidator createFamilyValidator,
        AddFamilyMemberValidator addMemberValidator,
        UpdateFamilyMemberValidator updateMemberValidator)
    {
        _db = db;
        _createFamilyValidator = createFamilyValidator;
        _addMemberValidator = addMemberValidator;
        _updateMemberValidator = updateMemberValidator;
    }

    // ── List all families ─────────────────────────────────────────────────────

    public async Task<List<FamilyDto>> ListFamiliesAsync(Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);

        var families = await _db.Families
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .OrderBy(f => f.Name)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Load all members in one query, group in memory.
        var familyIds = families.Select(f => f.Id).ToList();
        var allMembers = await _db.FamilyMembers
            .Where(m => familyIds.Contains(m.FamilyId) && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();

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

    public async Task<FamilyDto> GetFamilyAsync(Guid familyId, Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);
        return await BuildFamilyDtoAsync(familyId)
            ?? throw Throw422("FamilyId", "Family not found.");
    }

    // ── Create family ─────────────────────────────────────────────────────────

    public async Task<FamilyDto> CreateFamilyAsync(CreateFamilyRequest req, Guid callerId)
    {
        await _createFamilyValidator.ValidateAndThrowAsync(req);
        await RequireGlobalAdminAsync(callerId);

        var family = new Family
        {
            Id               = Guid.NewGuid(),
            Name             = req.Name.Trim(),
            CreatedByUserId  = callerId,
            CreatedAt        = DateTimeOffset.UtcNow,
            UpdatedAt        = DateTimeOffset.UtcNow
        };

        _db.Families.Add(family);
        await _db.SaveChangesAsync();

        return await BuildFamilyDtoAsync(family.Id)
            ?? throw new InvalidOperationException("Family not found after create.");
    }

    // ── Add member to a family ────────────────────────────────────────────────

    public async Task<FamilyMemberDto> AddFamilyMemberAsync(
        Guid familyId,
        AddFamilyMemberRequest req,
        Guid callerId)
    {
        await _addMemberValidator.ValidateAndThrowAsync(req);
        await RequireGlobalAdminAsync(callerId);

        var familyExists = await _db.Families.AnyAsync(f => f.Id == familyId);
        if (!familyExists)
            throw Throw422("FamilyId", "Family not found.");

        var emailNorm = req.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null)
        {
            var conflict = await _db.FamilyMembers
                .AnyAsync(m => m.IsActive && m.Email != null && m.Email.ToLower() == emailNorm);
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

        // Auto-link if a User with this email already exists.
        if (emailNorm is not null)
        {
            var existingUser = await _db.Users
                .Where(u => u.Email.ToLower() == emailNorm)
                .Select(u => new { u.Id })
                .FirstOrDefaultAsync();

            member.UserId = existingUser?.Id;
        }

        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync();

        return FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Update any member ─────────────────────────────────────────────────────

    public async Task<FamilyMemberDto> UpdateFamilyMemberAsync(
        Guid memberId,
        UpdateFamilyMemberRequest req,
        Guid callerId)
    {
        await _updateMemberValidator.ValidateAndThrowAsync(req);
        await RequireGlobalAdminAsync(callerId);

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.IsActive)
            .FirstOrDefaultAsync()
            ?? throw Throw422("MemberId", "Family member not found.");

        var emailNorm = req.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && emailNorm != member.Email)
        {
            var conflict = await _db.FamilyMembers
                .AnyAsync(m => m.IsActive && m.Id != memberId && m.Email != null && m.Email.ToLower() == emailNorm);
            if (conflict)
                throw Throw422("Email", "A family member with this email already exists.");
        }

        member.DisplayName    = req.DisplayName.Trim();
        member.Email          = emailNorm;
        member.DateOfBirth    = req.DateOfBirth;
        member.WeightOverride = req.WeightOverride;
        member.IsAdmin        = req.IsAdmin;

        await _db.SaveChangesAsync();
        return FamilyService.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Remove any member ─────────────────────────────────────────────────────

    public async Task RemoveFamilyMemberAsync(Guid memberId, Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.IsActive)
            .FirstOrDefaultAsync()
            ?? throw Throw422("MemberId", "Family member not found.");

        member.IsActive = false;
        await _db.SaveChangesAsync();
    }

    // ── Add a family to a group ───────────────────────────────────────────────

    public async Task AddFamilyToGroupAsync(Guid groupId, Guid familyId, Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);

        var groupExists = await _db.Groups.AnyAsync(g => g.Id == groupId);
        if (!groupExists)
            throw Throw422("GroupId", "Group not found.");

        var familyExists = await _db.Families.AnyAsync(f => f.Id == familyId);
        if (!familyExists)
            throw Throw422("FamilyId", "Family not found.");

        var alreadyMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == familyId);
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
        await _db.SaveChangesAsync();
    }

    // ── Remove a family from a group ──────────────────────────────────────────

    public async Task RemoveFamilyFromGroupAsync(Guid groupId, Guid familyId, Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);

        var groupFamily = await _db.GroupFamilies
            .Where(gf => gf.GroupId == groupId && gf.FamilyId == familyId)
            .FirstOrDefaultAsync()
            ?? throw Throw422("FamilyId", "This family is not in the group.");

        _db.GroupFamilies.Remove(groupFamily);
        await _db.SaveChangesAsync();
    }

    // ── Delete a group ────────────────────────────────────────────────────────

    public async Task DeleteGroupAsync(Guid groupId, Guid callerId)
    {
        await RequireGlobalAdminAsync(callerId);

        var deleted = await _db.Groups
            .Where(g => g.Id == groupId)
            .ExecuteDeleteAsync();

        if (deleted == 0)
            throw Throw422("GroupId", "Group not found.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RequireGlobalAdminAsync(Guid userId)
    {
        var isAdmin = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync();

        if (!isAdmin) throw Forbidden();
    }

    private async Task<FamilyDto?> BuildFamilyDtoAsync(Guid familyId)
    {
        var family = await _db.Families
            .Where(f => f.Id == familyId)
            .Select(f => new { f.Id, f.Name, f.CreatedAt, f.UpdatedAt })
            .FirstOrDefaultAsync();

        if (family is null) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var members = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();

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
