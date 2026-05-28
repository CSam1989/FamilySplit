using FluentValidation;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Families.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<FamilyService> _logger;

    public FamilyService(
        AppDbContext db,
        AddFamilyMemberValidator addValidator,
        UpdateFamilyMemberValidator updateValidator,
        UpdateFamilyNameValidator nameValidator,
        ILogger<FamilyService> logger)
    {
        _db = db;
        _addValidator = addValidator;
        _updateValidator = updateValidator;
        _nameValidator = nameValidator;
        _logger = logger;
    }

    // ── Get my family ─────────────────────────────────────────────────────────

    public async Task<FamilyDto?> GetMyFamilyAsync(Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetMyFamilyAsync called by {UserId}", callerId);
        var member = await GetCallerMemberAsync(callerId, ct);
        if (member is null) return null;
        return await BuildFamilyDtoAsync(member.FamilyId, ct);
    }

    // ── Get my profile ────────────────────────────────────────────────────────

    public async Task<FamilyMemberDto?> GetMyProfileAsync(Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetMyProfileAsync called by {UserId}", callerId);
        var member = await GetCallerMemberAsync(callerId, ct);
        if (member is null) return null;
        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Rename family (admin only) ────────────────────────────────────────────

    public async Task<FamilyDto> UpdateFamilyNameAsync(UpdateFamilyNameRequest req, Guid callerId, CancellationToken ct = default)
    {
        await _nameValidator.ValidateAndThrowAsync(req);
        var member = await GetCallerMemberOrThrowAsync(callerId, ct);
        if (!member.IsAdmin) throw Forbidden();

        var family = await _db.Families.FindAsync([member.FamilyId], ct)
            ?? throw Forbidden();

        family.Name      = req.Name.Trim();
        family.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Family {FamilyId} renamed by admin {UserId}", family.Id, callerId);

        return await BuildFamilyDtoAsync(family.Id, ct)
            ?? throw Forbidden();
    }

    // ── Add member (admin only) ───────────────────────────────────────────────

    public async Task<FamilyMemberDto> AddMemberAsync(AddFamilyMemberRequest req, Guid callerId, CancellationToken ct = default)
    {
        await _addValidator.ValidateAndThrowAsync(req);
        var caller = await GetCallerMemberOrThrowAsync(callerId, ct);
        if (!caller.IsAdmin) throw Forbidden();

        var emailNorm = req.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null)
        {
            // Email column stores the canonical lowercase form, so a direct
            // equality predicate hits the unique index.
            var conflict = await _db.FamilyMembers
                .AnyAsync(m => m.IsActive && m.Email == emailNorm, ct);
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

        // Auto-link if a User with this email already exists. User.Email is
        // also stored lowercase (normalized in OAuthHandler).
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
            "FamilyMember {MemberId} added to family {FamilyId} by {UserId} (linked: {IsLinked})",
            member.Id, caller.FamilyId, callerId, member.UserId is not null);

        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Update member (admin or self) ─────────────────────────────────────────

    public async Task<FamilyMemberDto> UpdateMemberAsync(
        Guid memberId,
        UpdateFamilyMemberRequest req,
        Guid callerId,
        CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(req);
        var caller = await GetCallerMemberOrThrowAsync(callerId, ct);

        // Allow if editing own profile; otherwise require admin.
        if (caller.Id != memberId && !caller.IsAdmin)
            throw Forbidden();

        // Target member must be in the same family.
        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.FamilyId == caller.FamilyId && m.IsActive)
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
        // Only family admins may change the IsAdmin flag; non-admins editing their
        // own profile cannot self-elevate or self-demote.
        if (caller.IsAdmin)
            member.IsAdmin = req.IsAdmin;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {MemberId} updated by {UserId}", memberId, callerId);

        return ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    // ── Remove member (admin only, cannot remove self) ────────────────────────

    public async Task RemoveMemberAsync(Guid memberId, Guid callerId, CancellationToken ct = default)
    {
        var caller = await GetCallerMemberOrThrowAsync(callerId, ct);
        if (!caller.IsAdmin) throw Forbidden();
        if (caller.Id == memberId)
            throw Throw422("MemberId", "You cannot remove yourself from the family.");

        var member = await _db.FamilyMembers
            .Where(m => m.Id == memberId && m.FamilyId == caller.FamilyId && m.IsActive)
            .FirstOrDefaultAsync(ct)
            ?? throw Throw422("MemberId", "Family member not found.");

        member.IsActive = false;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {MemberId} deactivated by {UserId}", memberId, callerId);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task<FamilyMember?> GetCallerMemberAsync(Guid userId, CancellationToken ct) =>
        await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .FirstOrDefaultAsync(ct);

    private async Task<FamilyMember> GetCallerMemberOrThrowAsync(Guid userId, CancellationToken ct) =>
        await GetCallerMemberAsync(userId, ct) ?? throw Forbidden();

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
