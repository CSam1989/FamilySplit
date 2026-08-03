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
        var caller = await _data.GetCallerMemberAsync(callerId, ct)
            ?? throw new ForbiddenException();
        if (!caller.IsAdmin)
            throw new ForbiddenException();
        if (caller.Id == memberId)
            throw ValidationErrors.Field("MemberId", "You cannot remove yourself from the family.");

        _ = await _data.GetActiveMemberInFamilyAsync(memberId, caller.FamilyId, ct)
            ?? throw ValidationErrors.Field("MemberId", "Family member not found.");

        await _data.DeactivateMemberAsync(memberId, ct);

        _logger.LogInformation("FamilyMember {MemberId} deactivated by {UserId}", memberId, callerId);
    }
}
