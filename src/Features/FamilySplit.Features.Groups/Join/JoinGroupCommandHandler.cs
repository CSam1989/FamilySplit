using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.Join;

/// <summary>
/// Command (business logic) — joins the caller-family to a group via its invite code. Holds no EF —
/// all data access goes through <see cref="IGroupData"/> (ADR-001). Returns the joined group id so
/// the client can navigate (200 + id — a documented strict-CQRS exception, since the client only
/// holds the invite code).
/// </summary>
public sealed class JoinGroupCommandHandler
{
    private readonly IGroupData _data;
    private readonly JoinGroupCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<JoinGroupCommandHandler> _logger;

    public JoinGroupCommandHandler(
        IGroupData data,
        JoinGroupCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<JoinGroupCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(JoinGroupCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Joining group by user {UserId}", callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (!await _data.IsActiveFamilyAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin join-group attempt by user {UserId}", callerId);
            throw new ForbiddenException();
        }

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);

        var groupId = await _data.GetGroupIdByInviteCodeAsync(cmd.InviteCode.ToUpperInvariant(), ct);
        if (groupId is null)
        {
            // Log invalid attempts so brute-force probing is visible in the logs.
            _logger.LogWarning("Invalid invite-code join attempt by user {UserId}", callerId);
            throw ValidationErrors.Field("InviteCode", "Invite code is invalid or has expired.");
        }

        if (await _data.IsFamilyInGroupAsync(groupId.Value, callerFamilyId, ct))
        {
            _logger.LogDebug("Join attempt for group {GroupId} by family {FamilyId} already a member", groupId.Value, callerFamilyId);
            throw ValidationErrors.Field("InviteCode", "Your family is already a member of this group.");
        }

        var membership = new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = groupId.Value,
            FamilyId = callerFamilyId,
            Role = MemberRole.Member,
        };

        await _data.AddFamilyToGroupAsync(membership, ct);

        _logger.LogInformation("Family joined group. {GroupId} {FamilyId} by {UserId}", groupId.Value, callerFamilyId, callerId);

        return groupId.Value;
    }
}
