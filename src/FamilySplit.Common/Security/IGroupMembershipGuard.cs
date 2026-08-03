namespace FamilySplit.Common.Security;

/// <summary>
/// Mockable seam for the caller-resolution / group-membership authorization guards.
/// Command handlers (business logic) depend on this interface so they can be unit-tested
/// with a mocked guard and never touch <c>AppDbContext</c> (ADR-001). The concrete
/// <see cref="GroupMembershipGuard"/> implementation is the data-access side and is
/// verified with Testcontainers.
/// </summary>
public interface IGroupMembershipGuard
{
    /// <summary>
    /// Resolves the caller's active FamilyId, or throws when the user has no active FamilyMember.
    /// </summary>
    Task<Guid> GetCallerFamilyIdAsync(Guid callerId, CancellationToken ct);

    /// <summary>
    /// Throws unless the caller's family belongs to the group.
    /// </summary>
    Task RequireGroupMemberAsync(Guid groupId, Guid callerId, CancellationToken ct);
}
