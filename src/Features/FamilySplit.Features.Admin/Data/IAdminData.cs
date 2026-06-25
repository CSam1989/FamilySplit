using FamilySplit.Domain.Entities;

namespace FamilySplit.Features.Admin.Data;

/// <summary>
/// The Admin slice data-access seam (ADR-001). All write-side EF/<c>AppDbContext</c> access lives
/// behind this interface so the command handlers (business logic) hold no EF and are unit-tested by
/// mocking it — including the global-admin check (<see cref="IsGlobalAdminAsync"/>), so command tests
/// stub the admin gate. Reads return plain values/records; writes accept the handler's computed state
/// and persist it. Admin mutations are non-financial, so there is no audit flush. The implementation
/// is Testcontainers-tested.
/// </summary>
public interface IAdminData
{
    // ── reads (plain values/records) ──────────────────────────────────────────────

    /// <summary>True if the caller's User row has <c>IsGlobalAdmin = true</c>.</summary>
    Task<bool> IsGlobalAdminAsync(Guid callerId, CancellationToken ct);

    Task<bool> FamilyExistsAsync(Guid familyId, CancellationToken ct);

    Task<bool> GroupExistsAsync(Guid groupId, CancellationToken ct);

    /// <summary>True if an active member (other than <paramref name="excludeMemberId"/>) already uses the email.</summary>
    Task<bool> EmailInUseAsync(string email, Guid? excludeMemberId, CancellationToken ct);

    /// <summary>The active member's identity + current email, or null if it does not exist / is inactive.</summary>
    Task<AdminMemberRecord?> GetActiveMemberAsync(Guid memberId, CancellationToken ct);

    /// <summary>The id of a User whose email matches (for auto-linking a new member), or null.</summary>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct);

    /// <summary>True if the family is currently a member of the group.</summary>
    Task<bool> FamilyInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct);

    // ── writes (each owns SaveChangesAsync) ───────────────────────────────────────

    Task AddFamilyAsync(Family family, CancellationToken ct);

    Task AddMemberAsync(FamilyMember member, CancellationToken ct);

    Task UpdateMemberAsync(Guid memberId, AdminMemberFields fields, CancellationToken ct);

    Task DeactivateMemberAsync(Guid memberId, CancellationToken ct);

    Task AddFamilyToGroupAsync(GroupFamily membership, CancellationToken ct);

    Task RemoveFamilyFromGroupAsync(Guid groupId, Guid familyId, CancellationToken ct);

    /// <summary>Hard-deletes a group (sub-activities first, then the cascade). Returns false if absent.</summary>
    Task<bool> DeleteGroupAsync(Guid groupId, CancellationToken ct);
}

/// <summary>An active member's identity + current email, used by the update/remove guards.</summary>
public sealed record AdminMemberRecord(Guid Id, Guid FamilyId, string? Email);

/// <summary>The new member field values to persist on update.</summary>
public sealed record AdminMemberFields(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin);
