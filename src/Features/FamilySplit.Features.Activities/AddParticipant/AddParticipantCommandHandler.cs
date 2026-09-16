using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.AddParticipant;

/// <summary>
/// Command (business logic) — adds an active group member as a participant in an open activity. Holds
/// no EF — all data access goes through <see cref="IActivityData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class AddParticipantCommandHandler
{
    private readonly IActivityData _data;
    private readonly AddParticipantCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<AddParticipantCommandHandler> _logger;

    public AddParticipantCommandHandler(
        IActivityData data,
        AddParticipantCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<AddParticipantCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid activityId, AddParticipantCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Adding participant {FamilyMemberId} to activity {ActivityId} by user {UserId}", cmd.FamilyMemberId, activityId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var activity = await _data.GetActivityCoreAsync(activityId, ct);
        if (activity is null)
        {
            _logger.LogDebug("Add-participant on missing activity {ActivityId} by user {UserId}", activityId, callerId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
        {
            _logger.LogDebug("Rejected add-participant on non-open activity {ActivityId} by user {UserId}", activityId, callerId);
            throw ValidationErrors.Field("Status", "Cannot add participants to a closed activity.");
        }

        if (!await _data.IsMemberInGroupAsync(activity.GroupId, cmd.FamilyMemberId, ct))
        {
            _logger.LogDebug("Rejected add-participant {FamilyMemberId} not in group for activity {ActivityId} by user {UserId}", cmd.FamilyMemberId, activityId, callerId);
            throw ValidationErrors.Field("FamilyMemberId", "Member is not part of any family in this group.");
        }

        if (await _data.IsParticipantAsync(activityId, cmd.FamilyMemberId, ct))
        {
            _logger.LogDebug("Rejected add-participant {FamilyMemberId} already a participant in activity {ActivityId} by user {UserId}", cmd.FamilyMemberId, activityId, callerId);
            throw ValidationErrors.Field("FamilyMemberId", "Member is already a participant in this activity.");
        }

        await _data.AddParticipantAsync(activityId, cmd.FamilyMemberId, ct);

        _logger.LogInformation("FamilyMember {FamilyMemberId} added as participant to activity {ActivityId} by user {UserId}", cmd.FamilyMemberId, activityId, callerId);
    }
}
