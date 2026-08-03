using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Features.Activities.Data;
using FamilySplit.Features.Activities.Shared;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.Close;

/// <summary>
/// Command (business logic) — closes a top-level activity; any open sub-activities are absorbed into
/// the parent by the data gateway. Holds no EF — all data access goes through
/// <see cref="IActivityData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class CloseActivityCommandHandler
{
    private readonly IActivityData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<CloseActivityCommandHandler> _logger;

    public CloseActivityCommandHandler(
        IActivityData data,
        IGroupMembershipGuard guard,
        ILogger<CloseActivityCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Closing activity {ActivityId} by user {UserId}", activityId, callerId);

        var activity = await _data.GetActivityCoreAsync(activityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (!ActivityCloseGuard.CanClose(activity.Status))
            throw ValidationErrors.Field("Status", "Activity is already closed or settled.");

        if (!ActivityCloseGuard.IsTopLevel(activity.ParentActivityId))
            throw ValidationErrors.Field("Status", "Sub-activities cannot be closed directly. Close the parent activity instead.");

        var absorbed = await _data.CloseActivityAsync(activityId, callerId, ct);

        _logger.LogInformation("Activity {ActivityId} closed by user {UserId}; {Count} sub-activities absorbed", activityId, callerId, absorbed);
    }
}
