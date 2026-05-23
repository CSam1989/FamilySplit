using FluentValidation;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Application.Families;

/// <summary>
/// Manages the Family that the currently authenticated user belongs to.
/// Family admins (IsAdmin = true on their FamilyMember) can add / update / remove
/// other members. Any member can read their own family and update their own profile.
/// </summary>
public class FamilyService
{
    private readonly AppDbContext _db;
    private readonly AddFamilyMemberValidator _addValidator;
    private readonly UpdateFamilyMemberValidator _updateValidator;
    private readonly UpdateFamilyNameValidator _nameValidator;

    public FamilyService(
        AppDbContext db,
        AddFamilyMemberValidator addValidator,
        UpdateFamilyMemberValidator updateValidator,
        UpdateFamilyNameValidator nameValidator)
    {
        _db = db;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
        _nameValidator = nameValidator;
    }

    // ── Get my family ─────────────────────────────────────────────────────────

    public async Task<FamilyDto?> GetMyFamilyAsync(Guid callerId)
    {
        var member = await GetCallerMemberAsync(callerId);
        if (member is null) return null;
        return await BuildFamilyDtoAsync(member.FamilyId);
    }

    // ── Get my profile ────────────────────────────────────────────────────────

    public async Task<FamilyMemberDto?> GetMyProfileAsync(Guid callerId)
    {
        var member = await GetCallerMemberAsync(callerId);
        if (member is null) return null;
        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Rename family (admin only) ────────────────────────────────────────────

    public async Task<FamilyDto> UpdateFamilyNameAsync(UpdateFamilyNameRequest req, Guid callerId)
    {
        await _nameValidator.ValidateAndThrowAsync(req);
        var member = await GetCallerMemberOrThrowAsync(callerId);
        if (!member.IsAdmin) throw Forbidden();

        var family = await _db.Families.FindAsync(member.FamilyId)
            ?? throw Forbidden();

        family.Name      = req.Name.Trim();
        family.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        return await BuildFamilyDtoAsync(family.Id)
            ?? throw Forbidden();
    }

    // ── Add member (admin only) ───────────────────────────────────────────────

    public async Task<FamilyMemberDto> AddMemberAsync(AddFamilyMemberRequest req, Guid callerId)
    {
        await _addValidator.ValidateAndThrowAsync(req);
        var caller = await GetCallerMemberOrThrowAsync(callerId);
        if (!caller.IsAdmin) throw Forbidden();

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
            FamilyId       = caller.FamilyId,
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

        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Update member (admin or self) ─────────────────────────────────────────

    public async Task<FamilyMemberDto> UpdateMemberAsync(
        Guid memberId,
        UpdateFamilyMemberRequest req,
        Guid callerId)
    {
        await _updateValidator.ValidateAndThrowAsync(req);
        var caller = await GetCallerMemberOrThrowAsync(callerId);

        // Allow if editing own profile; otherwise require admin.
        if (caller.Id != memberId && !caller.IsAdmin)
            throw Forbidden();

        // Target member must be in the same family.
        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.FamilyId == caller.FamilyId && m.IsActive)
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
        // Only family admins may change the IsAdmin flag; non-admins editing their
        // own profile cannot self-elevate or self-demote.
        if (caller.IsAdmin)
            member.IsAdmin = req.IsAdmin;

        await _db.SaveChangesAsync();
        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Remove member (admin only, cannot remove self) ────────────────────────

    public async Task RemoveMemberAsync(Guid memberId, Guid callerId)
    {
        var caller = await GetCallerMemberOrThrowAsync(callerId);
        if (!caller.IsAdmin) throw Forbidden();
        if (caller.Id == memberId)
            throw Throw422("MemberId", "You cannot remove yourself from the family.");

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.FamilyId == caller.FamilyId && m.IsActive)
            .FirstOrDefaultAsync()
            ?? throw Throw422("MemberId", "Family member not found.");

        member.IsActive = false;
        await _db.SaveChangesAsync();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<FamilyMember?> GetCallerMemberAsync(Guid userId) =>
        await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .FirstOrDefaultAsync();

    private async Task<FamilyMember> GetCallerMemberOrThrowAsync(Guid userId) =>
        await GetCallerMemberAsync(userId) ?? throw Forbidden();

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
            members.Select(m => ToDto(m, today)).ToList(),
            family.CreatedAt,
            family.UpdatedAt);
    }

    internal static FamilyMemberDto ToDto(FamilyMember m, DateOnly asOfDate) => new(
        Id:             m.Id,
        DisplayName:    m.DisplayName,
        Email:          m.Email,
        DateOfBirth:    m.DateOfBirth,
        WeightOverride: m.WeightOverride,
        CurrentWeight:  WeightCalculator.GetWeight(m, asOfDate),
        CurrentTier:    WeightCalculator.GetTier(m, asOfDate),
        IsActive:       m.IsActive,
        IsLinked:       m.UserId is not null,
        IsAdmin:        m.IsAdmin,
        CreatedAt:      m.CreatedAt);

    private static ForbiddenException Forbidden() => new();
    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
