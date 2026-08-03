using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.RemoveParticipant;

/// <summary>
/// Command (business logic) — removes a participant from an open activity. Holds no EF — all data
/// access goes through <see cref="IActivityData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class RemoveParticipantCommandHandler
{
    private readonly IActivityData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<RemoveParticipantCommandHandler> _logger;

    public RemoveParticipantCommandHandler(
        IActivityData data,
        IGroupMembershipGuard guard,
        ILogger<RemoveParticipantCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid activityId, Guid familyMemberId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Removing participant {FamilyMemberId} from activity {ActivityId} by user {UserId}", familyMemberId, activityId, callerId);

        var activity = await _data.GetActivityCoreAsync(activityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
            throw ValidationErrors.Field("Status", "Cannot remove participants from a closed activity.");

        if (!await _data.IsParticipantAsync(activityId, familyMemberId, ct))
            throw ValidationErrors.Field("FamilyMemberId", "Member is not a participant in this activity.");

        await _data.RemoveParticipantAsync(activityId, familyMemberId, ct);

        _logger.LogInformation("FamilyMember {FamilyMemberId} removed as participant from activity {ActivityId} by user {UserId}", familyMemberId, activityId, callerId);
    }
}
