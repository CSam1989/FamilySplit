using FamilySplit.Domain.Entities;

namespace FamilySplit.Features.Families.Data;

/// <summary>
/// The Families slice data-access seam (ADR-001). All write-side EF/<c>AppDbContext</c> access lives
/// behind this interface so the command handlers (business logic) hold no EF and are unit-tested by
/// mocking it — including resolving the caller's own <see cref="FamilyCallerMember"/> (the admin-or-self
/// authorization decisions stay in the handlers). Reads return plain values/records; writes accept the
/// handler's computed state and persist it. Family mutations are non-financial, so there is no audit
/// flush. The implementation is Testcontainers-tested.
/// </summary>
public interface IFamilyData
{
    // ── reads (plain values/records) ──────────────────────────────────────────────

    /// <summary>The caller's own active FamilyMember identity, or null if none is linked.</summary>
    Task<FamilyCallerMember?> GetCallerMemberAsync(Guid callerId, CancellationToken ct);

    /// <summary>True if an active member (other than <paramref name="excludeMemberId"/>) already uses the email.</summary>
    Task<bool> EmailInUseAsync(string email, Guid? excludeMemberId, CancellationToken ct);

    /// <summary>The active member's identity + current email/admin flag, scoped to <paramref name="familyId"/>.</summary>
    Task<FamilyMemberRecord?> GetActiveMemberInFamilyAsync(Guid memberId, Guid familyId, CancellationToken ct);

    /// <summary>The id of a User whose email matches (for auto-linking a new member), or null.</summary>
    Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken ct);

    // ── writes (each owns SaveChangesAsync) ───────────────────────────────────────

    Task UpdateFamilyNameAsync(Guid familyId, string name, CancellationToken ct);

    Task AddMemberAsync(FamilyMember member, CancellationToken ct);

    Task UpdateMemberAsync(Guid memberId, FamilyMemberFields fields, CancellationToken ct);

    Task DeactivateMemberAsync(Guid memberId, CancellationToken ct);
}

/// <summary>The caller's own FamilyMember identity, used by every command's admin-or-self guard.</summary>
public sealed record FamilyCallerMember(Guid Id, Guid FamilyId, bool IsAdmin);

/// <summary>A target member's identity + current email/admin flag, used by the update/remove guards.</summary>
public sealed record FamilyMemberRecord(Guid Id, Guid FamilyId, string? Email, bool IsAdmin);

/// <summary>The new member field values to persist on update.</summary>
public sealed record FamilyMemberFields(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin);
