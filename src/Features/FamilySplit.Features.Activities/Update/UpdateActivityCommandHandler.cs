using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.Update;

/// <summary>
/// Command (business logic) — renames / re-describes an open activity. Holds no EF — all data access
/// goes through <see cref="IActivityData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateActivityCommandHandler
{
    private readonly IActivityData _data;
    private readonly UpdateActivityCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<UpdateActivityCommandHandler> _logger;

    public UpdateActivityCommandHandler(
        IActivityData data,
        UpdateActivityCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<UpdateActivityCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid activityId, UpdateActivityCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Updating activity {ActivityId} by user {UserId}", activityId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var activity = await _data.GetActivityCoreAsync(activityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
            throw ValidationErrors.Field("Status", "Only open activities can be edited.");

        await _data.UpdateActivityDetailsAsync(activityId, cmd.Name.Trim(), cmd.Description?.Trim(), ct);

        _logger.LogInformation("Activity {ActivityId} updated by user {UserId}", activityId, callerId);
    }
}
