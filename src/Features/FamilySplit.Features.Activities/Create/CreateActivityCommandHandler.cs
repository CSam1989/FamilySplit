using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Data;
using FamilySplit.Features.Activities.Shared;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.Create;

/// <summary>
/// Command (business logic) — creates a top-level activity and seeds its participants from all active
/// members of every family in the group. Holds no EF — all data access goes through
/// <see cref="IActivityData"/> (ADR-001). Returns the new activity id (201 + id).
/// </summary>
public sealed class CreateActivityCommandHandler
{
    private readonly IActivityData _data;
    private readonly CreateActivityCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<CreateActivityCommandHandler> _logger;

    public CreateActivityCommandHandler(
        IActivityData data,
        CreateActivityCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<CreateActivityCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(Guid groupId, CreateActivityCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating activity in group {GroupId} by user {UserId}", groupId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);
        await _guard.RequireGroupMemberAsync(groupId, callerId, ct);

        var now = DateTimeOffset.UtcNow;
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            CreatedByUserId = callerId,
            Status = ActivityStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var memberIds = await _data.GetActiveGroupMemberIdsAsync(groupId, ct);
        var participants = ParticipantSeeder.SeedForActivity(activity.Id, memberIds);

        await _data.PersistNewActivityAsync(activity, participants, ct);

        _logger.LogInformation("Activity {ActivityId} created in group {GroupId} by user {UserId}", activity.Id, groupId, callerId);

        return activity.Id;
    }
}
