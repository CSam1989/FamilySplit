using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.DeleteGroup;

/// <summary>
/// Command (business logic) — global-admin hard-deletes a group (cascades to members, activities). The
/// transactional delete itself lives in the gateway; the handler enforces the admin gate and maps the
/// "not found" result to 422. Holds no EF — all data access goes through <see cref="IAdminData"/>
/// (ADR-001). Returns nothing (204).
/// </summary>
public sealed class DeleteGroupCommandHandler
{
    private readonly IAdminData _data;
    private readonly ILogger<DeleteGroupCommandHandler> _logger;

    public DeleteGroupCommandHandler(IAdminData data, ILogger<DeleteGroupCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        if (!await _data.IsGlobalAdminAsync(callerId, ct))
            throw new ForbiddenException();

        if (!await _data.DeleteGroupAsync(groupId, ct))
            throw ValidationErrors.Field("GroupId", "Group not found.");

        _logger.LogWarning("Group {GroupId} deleted by global admin {UserId}", groupId, callerId);
    }
}
