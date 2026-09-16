using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.Update;

/// <summary>
/// Command (business logic) — renames / re-describes a group. Admin-only. Holds no EF — all data
/// access goes through <see cref="IGroupData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateGroupCommandHandler
{
    private readonly IGroupData _data;
    private readonly UpdateGroupCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<UpdateGroupCommandHandler> _logger;

    public UpdateGroupCommandHandler(
        IGroupData data,
        UpdateGroupCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<UpdateGroupCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, UpdateGroupCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Updating group {GroupId} by user {UserId}", groupId, callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);
        var role = await _data.GetFamilyRoleInGroupAsync(groupId, callerFamilyId, ct);
        if (role != MemberRole.Admin)
        {
            _logger.LogWarning("Non-admin group-update attempt for group {GroupId} by user {UserId}", groupId, callerId);
            throw new ForbiddenException();
        }

        await _data.UpdateGroupDetailsAsync(groupId, cmd.Name.Trim(), cmd.Description?.Trim(), ct);

        _logger.LogInformation("Group updated. {GroupId} by {UserId}", groupId, callerId);
    }
}
