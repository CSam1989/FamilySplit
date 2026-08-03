using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.RegenerateInviteCode;

/// <summary>
/// Command (business logic) — rotates a group's invite code (the old code stops working). Admin-only.
/// Holds no EF — all data access goes through <see cref="IGroupData"/> (ADR-001). Returns nothing
/// (204); the client re-queries the group detail, which carries the new code for admins.
/// </summary>
public sealed class RegenerateInviteCodeCommandHandler
{
    private readonly IGroupData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<RegenerateInviteCodeCommandHandler> _logger;

    public RegenerateInviteCodeCommandHandler(
        IGroupData data,
        IGroupMembershipGuard guard,
        ILogger<RegenerateInviteCodeCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Regenerating invite code for group {GroupId} by user {UserId}", groupId, callerId);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);
        var role = await _data.GetFamilyRoleInGroupAsync(groupId, callerFamilyId, ct);
        if (role != MemberRole.Admin)
            throw new ForbiddenException();

        var newCode = await _data.GenerateUniqueInviteCodeAsync(ct);
        await _data.UpdateInviteCodeAsync(groupId, newCode, ct);

        _logger.LogWarning("Invite code regenerated (old code is now invalid). {GroupId} by {UserId}", groupId, callerId);
    }
}
