using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Create;
using FamilySplit.Features.Activities.Data;
using FamilySplit.Features.Activities.Shared;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.CreateSubActivity;

/// <summary>
/// Command (business logic) — creates a depth-1 sub-activity under an open top-level parent, seeding
/// its participants by copying the parent's. Reuses <see cref="CreateActivityCommand"/> (same request
/// shape). Holds no EF — all data access goes through <see cref="IActivityData"/> (ADR-001). Returns
/// the new sub-activity id (201 + id).
/// </summary>
public sealed class CreateSubActivityCommandHandler
{
    private readonly IActivityData _data;
    private readonly CreateActivityCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<CreateSubActivityCommandHandler> _logger;

    public CreateSubActivityCommandHandler(
        IActivityData data,
        CreateActivityCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<CreateSubActivityCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(Guid parentActivityId, CreateActivityCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating sub-activity under parent {ActivityId} by user {UserId}", parentActivityId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var parent = await _data.GetActivityCoreAsync(parentActivityId, ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        // Depth-1 guard: sub-activities may not themselves have a parent.
        if (parent.ParentActivityId is not null)
            throw ValidationErrors.Field("ParentActivityId", "Sub-activities cannot be nested more than one level deep.");

        if (parent.Status != ActivityStatus.Open)
            throw ValidationErrors.Field("Status", "Cannot add a sub-activity to a closed or settled activity.");

        await _guard.RequireGroupMemberAsync(parent.GroupId, callerId, ct);

        var now = DateTimeOffset.UtcNow;
        var sub = new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = parent.GroupId,
            ParentActivityId = parentActivityId,
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            CreatedByUserId = callerId,
            Status = ActivityStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var parentMemberIds = await _data.GetActivityParticipantMemberIdsAsync(parentActivityId, ct);
        var participants = ParticipantSeeder.SeedForSubActivity(sub.Id, parentMemberIds);

        await _data.PersistNewActivityAsync(sub, participants, ct);

        _logger.LogInformation("Sub-activity {ActivityId} created under parent {ParentActivityId} by user {UserId}", sub.Id, parentActivityId, callerId);

        return sub.Id;
    }
}
