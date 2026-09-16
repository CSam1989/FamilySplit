using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.RemoveMember;

/// <summary>
/// Command (business logic) — a family admin soft-deletes a member of their own family
/// (<c>IsActive = false</c>); the caller cannot remove themselves. Holds no EF — all data access goes
/// through <see cref="IFamilyData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class RemoveMemberCommandHandler
{
    private readonly IFamilyData _data;
    private readonly ILogger<RemoveMemberCommandHandler> _logger;

    public RemoveMemberCommandHandler(IFamilyData data, ILogger<RemoveMemberCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(Guid memberId, Guid callerId, CancellationToken ct)
    {
        var caller = await _data.GetCallerMemberAsync(callerId, ct);
        if (caller is null)
        {
            _logger.LogWarning("Remove-member attempt by user {UserId} with no linked FamilyMember", callerId);
            throw new ForbiddenException();
        }
        if (!caller.IsAdmin)
        {
            _logger.LogWarning("Non-admin remove-member attempt by user {UserId}", callerId);
            throw new ForbiddenException();
        }
        if (caller.Id == memberId)
        {
            _logger.LogDebug("Self-remove attempt by user {UserId}", callerId);
            throw ValidationErrors.Field("MemberId", "You cannot remove yourself from the family.");
        }

        var target = await _data.GetActiveMemberInFamilyAsync(memberId, caller.FamilyId, ct);
        if (target is null)
        {
            _logger.LogDebug("Remove-member attempt by user {UserId} for unknown member {MemberId}", callerId, memberId);
            throw ValidationErrors.Field("MemberId", "Family member not found.");
        }

        await _data.DeactivateMemberAsync(memberId, ct);

        _logger.LogInformation("FamilyMember {MemberId} deactivated by {UserId}", memberId, callerId);
    }
}
