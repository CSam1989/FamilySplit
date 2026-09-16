using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.UpdateFamilyMember;

/// <summary>
/// Command (business logic) — global-admin updates any family member. Holds no EF — all data access
/// goes through <see cref="IAdminData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateFamilyMemberCommandHandler
{
    private readonly IAdminData _data;
    private readonly UpdateFamilyMemberCommandValidator _validator;
    private readonly ILogger<UpdateFamilyMemberCommandHandler> _logger;

    public UpdateFamilyMemberCommandHandler(
        IAdminData data,
        UpdateFamilyMemberCommandValidator validator,
        ILogger<UpdateFamilyMemberCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task HandleAsync(Guid memberId, UpdateFamilyMemberCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (!await _data.IsGlobalAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to update family member {MemberId}", callerId, memberId);
            throw new ForbiddenException();
        }

        var member = await _data.GetActiveMemberAsync(memberId, ct);
        if (member is null)
        {
            _logger.LogDebug("Update attempted on missing family member {MemberId} by user {UserId}", memberId, callerId);
            throw ValidationErrors.Field("MemberId", "Family member not found.");
        }

        var emailNorm = cmd.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && emailNorm != member.Email
            && await _data.EmailInUseAsync(emailNorm, excludeMemberId: memberId, ct))
        {
            _logger.LogDebug("Rejected update of family member {MemberId} — email already in use, by user {UserId}", memberId, callerId);
            throw ValidationErrors.Field("Email", "A family member with this email already exists.");
        }

        await _data.UpdateMemberAsync(memberId, new AdminMemberFields(
            cmd.DisplayName.Trim(), emailNorm, cmd.DateOfBirth, cmd.WeightOverride, cmd.IsAdmin), ct);

        _logger.LogInformation("FamilyMember {MemberId} updated by global admin {UserId}", memberId, callerId);
    }
}
