using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Families.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.AddMember;

/// <summary>
/// Command (business logic) — a family admin adds a member to their own family. Auto-links a matching
/// User by email. Holds no EF — all data access goes through <see cref="IFamilyData"/> (ADR-001).
/// Returns the new member id (201).
/// </summary>
public sealed class AddMemberCommandHandler
{
    private readonly IFamilyData _data;
    private readonly AddMemberCommandValidator _validator;
    private readonly ILogger<AddMemberCommandHandler> _logger;

    public AddMemberCommandHandler(
        IFamilyData data,
        AddMemberCommandValidator validator,
        ILogger<AddMemberCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(AddMemberCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var caller = await _data.GetCallerMemberAsync(callerId, ct)
            ?? throw new ForbiddenException();
        if (!caller.IsAdmin)
            throw new ForbiddenException();

        var emailNorm = cmd.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && await _data.EmailInUseAsync(emailNorm, excludeMemberId: null, ct))
            throw ValidationErrors.Field("Email", "A family member with this email already exists.");

        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            FamilyId = caller.FamilyId,
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
            "FamilyMember {MemberId} added to family {FamilyId} by admin {UserId} (linked: {IsLinked})",
            member.Id, caller.FamilyId, callerId, member.UserId is not null);

        return member.Id;
    }
}
