using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Admin.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.AddFamilyMember;

/// <summary>
/// Command (business logic) — global-admin adds a member to a family. Auto-links a matching User by
/// email. Holds no EF — all data access goes through <see cref="IAdminData"/> (ADR-001). Returns the
/// new member id (201).
/// </summary>
public sealed class AddFamilyMemberCommandHandler
{
    private readonly IAdminData _data;
    private readonly AddFamilyMemberCommandValidator _validator;
    private readonly ILogger<AddFamilyMemberCommandHandler> _logger;

    public AddFamilyMemberCommandHandler(
        IAdminData data,
        AddFamilyMemberCommandValidator validator,
        ILogger<AddFamilyMemberCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(Guid familyId, AddFamilyMemberCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (!await _data.IsGlobalAdminAsync(callerId, ct))
            throw new ForbiddenException();

        if (!await _data.FamilyExistsAsync(familyId, ct))
            throw ValidationErrors.Field("FamilyId", "Family not found.");

        var emailNorm = cmd.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && await _data.EmailInUseAsync(emailNorm, excludeMemberId: null, ct))
            throw ValidationErrors.Field("Email", "A family member with this email already exists.");

        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            Email = emailNorm,
            UserId = null,
            IsAdmin = cmd.IsAdmin,
            DisplayName = cmd.DisplayName.Trim(),
            DateOfBirth = cmd.DateOfBirth,
            WeightOverride = cmd.WeightOverride,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Auto-link if a User with this email already exists (User.Email is stored lowercased).
        if (emailNorm is not null)
            member.UserId = await _data.FindUserIdByEmailAsync(emailNorm, ct);

        await _data.AddMemberAsync(member, ct);

        _logger.LogInformation(
            "FamilyMember {MemberId} added to family {FamilyId} by global admin {UserId} (linked: {IsLinked})",
            member.Id, familyId, callerId, member.UserId is not null);

        return member.Id;
    }
}
