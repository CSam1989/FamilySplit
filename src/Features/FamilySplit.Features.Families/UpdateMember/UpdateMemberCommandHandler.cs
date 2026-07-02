using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.UpdateMember;

/// <summary>
/// Command (business logic) — updates a family member. Allowed for a family admin (any member) or the
/// member editing their own profile; only an admin caller may change the target's <c>IsAdmin</c> flag
/// (a non-admin editing themselves cannot self-elevate). Holds no EF — all data access goes through
/// <see cref="IFamilyData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateMemberCommandHandler
{
    private readonly IFamilyData _data;
    private readonly UpdateMemberCommandValidator _validator;
    private readonly ILogger<UpdateMemberCommandHandler> _logger;

    public UpdateMemberCommandHandler(
        IFamilyData data,
        UpdateMemberCommandValidator validator,
        ILogger<UpdateMemberCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task HandleAsync(Guid memberId, UpdateMemberCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var caller = await _data.GetCallerMemberAsync(callerId, ct)
            ?? throw new ForbiddenException();

        // Allow if editing own profile; otherwise require admin.
        if (caller.Id != memberId && !caller.IsAdmin)
            throw new ForbiddenException();

        var target = await _data.GetActiveMemberInFamilyAsync(memberId, caller.FamilyId, ct)
            ?? throw ValidationErrors.Field("MemberId", "Family member not found.");

        var emailNorm = cmd.Email?.Trim().ToLowerInvariant();

        if (emailNorm is not null && emailNorm != target.Email
            && await _data.EmailInUseAsync(emailNorm, excludeMemberId: memberId, ct))
            throw ValidationErrors.Field("Email", "A family member with this email already exists.");

        // Only family admins may change the IsAdmin flag; non-admins editing their own
        // profile cannot self-elevate or self-demote — keep the target's current flag.
        var effectiveIsAdmin = caller.IsAdmin ? cmd.IsAdmin : target.IsAdmin;

        await _data.UpdateMemberAsync(memberId, new FamilyMemberFields(
            cmd.DisplayName.Trim(), emailNorm, cmd.DateOfBirth, cmd.WeightOverride, effectiveIsAdmin), ct);

        _logger.LogInformation("FamilyMember {MemberId} updated by {UserId}", memberId, callerId);
    }
}
