using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.RemoveFamilyFromGroup;

/// <summary>
/// Command (business logic) — global-admin removes a family from a group. Holds no EF — all data
/// access goes through <see cref="IAdminData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class RemoveFamilyFromGroupCommandHandler
{
    private readonly IAdminData _data;
    private readonly ILogger<RemoveFamilyFromGroupCommandHandler> _logger;

    public RemoveFamilyFromGroupCommandHandler(IAdminData data, ILogger<RemoveFamilyFromGroupCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, Guid familyId, Guid callerId, CancellationToken ct)
    {
        if (!await _data.IsGlobalAdminAsync(callerId, ct))
            throw new ForbiddenException();

        if (!await _data.FamilyInGroupAsync(groupId, familyId, ct))
            throw ValidationErrors.Field("FamilyId", "This family is not in the group.");

        await _data.RemoveFamilyFromGroupAsync(groupId, familyId, ct);

        _logger.LogWarning("Family {FamilyId} removed from group {GroupId} by global admin {UserId}", familyId, groupId, callerId);
    }
}
