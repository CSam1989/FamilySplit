using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Admin.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.AddFamilyToGroup;

/// <summary>
/// Command (business logic) — global-admin adds a family to a group (as a Member). Holds no EF — all
/// data access goes through <see cref="IAdminData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class AddFamilyToGroupCommandHandler
{
    private readonly IAdminData _data;
    private readonly ILogger<AddFamilyToGroupCommandHandler> _logger;

    public AddFamilyToGroupCommandHandler(IAdminData data, ILogger<AddFamilyToGroupCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, AddFamilyToGroupCommand cmd, Guid callerId, CancellationToken ct)
    {
        if (!await _data.IsGlobalAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to add family {FamilyId} to group {GroupId}", callerId, cmd.FamilyId, groupId);
            throw new ForbiddenException();
        }

        if (!await _data.GroupExistsAsync(groupId, ct))
        {
            _logger.LogDebug("Add-family-to-group attempted on missing group {GroupId} by user {UserId}", groupId, callerId);
            throw ValidationErrors.Field("GroupId", "Group not found.");
        }

        if (!await _data.FamilyExistsAsync(cmd.FamilyId, ct))
        {
            _logger.LogDebug("Add-family-to-group attempted with missing family {FamilyId} by user {UserId}", cmd.FamilyId, callerId);
            throw ValidationErrors.Field("FamilyId", "Family not found.");
        }

        if (await _data.FamilyInGroupAsync(groupId, cmd.FamilyId, ct))
        {
            _logger.LogDebug("Rejected add-family-to-group — family {FamilyId} already in group {GroupId}, by user {UserId}", cmd.FamilyId, groupId, callerId);
            throw ValidationErrors.Field("FamilyId", "This family is already in the group.");
        }

        await _data.AddFamilyToGroupAsync(new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FamilyId = cmd.FamilyId,
            Role = MemberRole.Member,
            JoinedAt = DateTimeOffset.UtcNow,
        }, ct);

        _logger.LogInformation("Family {FamilyId} added to group {GroupId} by global admin {UserId}", cmd.FamilyId, groupId, callerId);
    }
}
