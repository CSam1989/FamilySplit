using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.Leave;

/// <summary>
/// Command (business logic) — removes the caller-family from a group. Family-admin only; refuses
/// when the caller's family is the group's sole Admin. Holds no EF — all data access goes through
/// <see cref="IGroupData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class LeaveGroupCommandHandler
{
    private readonly IGroupData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<LeaveGroupCommandHandler> _logger;

    public LeaveGroupCommandHandler(
        IGroupData data,
        IGroupMembershipGuard guard,
        ILogger<LeaveGroupCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Leaving group {GroupId} by user {UserId}", groupId, callerId);

        if (!await _data.IsActiveFamilyAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin leave-group attempt for group {GroupId} by user {UserId}", groupId, callerId);
            throw new ForbiddenException();
        }

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);

        var membership = await _data.GetFamilyMembershipAsync(groupId, callerFamilyId, ct);
        if (membership is null)
        {
            _logger.LogDebug("Leave attempt for group {GroupId} by non-member family {FamilyId}", groupId, callerFamilyId);
            throw ValidationErrors.Field("Group", "Your family is not a member of this group.");
        }

        // Guard: if this family is the sole admin, refuse the leave.
        if (membership.Role == MemberRole.Admin)
        {
            var adminCount = await _data.CountGroupAdminsAsync(groupId, ct);
            if (adminCount <= 1)
            {
                _logger.LogDebug("Sole-admin leave attempt for group {GroupId} by family {FamilyId}", groupId, callerFamilyId);
                throw ValidationErrors.Field("Group",
                    "Cannot leave: your family is the only admin of this group. " +
                    "Transfer the admin role to another family first.");
            }
        }

        await _data.RemoveFamilyFromGroupAsync(membership.GroupFamilyId, ct);

        _logger.LogInformation("Family left group. {GroupId} {FamilyId} by {UserId}", groupId, callerFamilyId, callerId);
    }
}
