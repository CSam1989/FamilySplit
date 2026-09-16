using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Groups.Create;

/// <summary>
/// Command (business logic) — creates a group and the caller-family's Admin membership. Holds no
/// EF — all data access goes through <see cref="IGroupData"/> (ADR-001). Returns the new group id
/// (201 + id).
/// </summary>
public sealed class CreateGroupCommandHandler
{
    private readonly IGroupData _data;
    private readonly CreateGroupCommandValidator _validator;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<CreateGroupCommandHandler> _logger;

    public CreateGroupCommandHandler(
        IGroupData data,
        CreateGroupCommandValidator validator,
        IGroupMembershipGuard guard,
        ILogger<CreateGroupCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _guard = guard;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateGroupCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Creating group by user {UserId}", callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (!await _data.IsActiveFamilyAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin group-create attempt by user {UserId}", callerId);
            throw new ForbiddenException();
        }

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);

        var groupId = Guid.NewGuid();

        var group = new Group
        {
            Id = groupId,
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            InviteCode = await _data.GenerateUniqueInviteCodeAsync(ct),
            CreatedByUserId = callerId,
        };

        var adminMembership = new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FamilyId = callerFamilyId,
            Role = MemberRole.Admin,
        };

        await _data.AddGroupAsync(group, adminMembership, ct);

        _logger.LogInformation("Group created. {GroupId} by {UserId}", groupId, callerId);

        return groupId;
    }
}
