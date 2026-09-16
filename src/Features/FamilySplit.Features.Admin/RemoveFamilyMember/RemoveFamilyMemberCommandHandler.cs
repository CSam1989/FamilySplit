using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.RemoveFamilyMember;

/// <summary>
/// Command (business logic) — global-admin soft-deletes any family member (<c>IsActive = false</c>).
/// Holds no EF — all data access goes through <see cref="IAdminData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class RemoveFamilyMemberCommandHandler
{
    private readonly IAdminData _data;
    private readonly ILogger<RemoveFamilyMemberCommandHandler> _logger;

    public RemoveFamilyMemberCommandHandler(IAdminData data, ILogger<RemoveFamilyMemberCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(Guid memberId, Guid callerId, CancellationToken ct)
    {
        if (!await _data.IsGlobalAdminAsync(callerId, ct))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to remove family member {MemberId}", callerId, memberId);
            throw new ForbiddenException();
        }

        if (await _data.GetActiveMemberAsync(memberId, ct) is null)
        {
            _logger.LogDebug("Remove attempted on missing family member {MemberId} by user {UserId}", memberId, callerId);
            throw ValidationErrors.Field("MemberId", "Family member not found.");
        }

        await _data.DeactivateMemberAsync(memberId, ct);

        _logger.LogInformation("FamilyMember {MemberId} deactivated by global admin {UserId}", memberId, callerId);
    }
}
