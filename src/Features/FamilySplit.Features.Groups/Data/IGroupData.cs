using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Groups.Data;

/// <summary>
/// The Groups slice data-access seam (ADR-001). All EF/<c>AppDbContext</c> access for the write
/// side lives behind this interface so the command handlers (business logic) hold no EF and are
/// unit-tested by mocking it. Reads return plain values/records — never tracked entities. Writes
/// accept the command handler's computed state and persist it. Group mutations are non-financial,
/// so there is no audit flush here (CLAUDE.md). The implementation is Testcontainers-tested through
/// the group endpoints.
/// </summary>
public interface IGroupData
{
    // ── authorization reads (plain values) ───────────────────────────────────────

    /// <summary>True if the caller has an active FamilyMember flagged <c>IsAdmin</c>.</summary>
    Task<bool> IsActiveFamilyAdminAsync(Guid callerId, CancellationToken ct);

    /// <summary>The caller-family's role within the group, or null if the family is not a member.</summary>
    Task<MemberRole?> GetFamilyRoleInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct);

    // ── domain reads ─────────────────────────────────────────────────────────────

    /// <summary>The id of the group whose invite code matches, or null.</summary>
    Task<Guid?> GetGroupIdByInviteCodeAsync(string inviteCode, CancellationToken ct);

    /// <summary>True if the family already participates in the group.</summary>
    Task<bool> IsFamilyInGroupAsync(Guid groupId, Guid familyId, CancellationToken ct);

    /// <summary>The family's membership row in the group (for the leave guard), or null.</summary>
    Task<GroupMembershipInfo?> GetFamilyMembershipAsync(Guid groupId, Guid familyId, CancellationToken ct);

    /// <summary>How many families hold the Admin role in the group.</summary>
    Task<int> CountGroupAdminsAsync(Guid groupId, CancellationToken ct);

    /// <summary>A fresh invite code guaranteed unique against the current rows.</summary>
    Task<string> GenerateUniqueInviteCodeAsync(CancellationToken ct);

    // ── writes ───────────────────────────────────────────────────────────────────

    Task AddGroupAsync(Group group, GroupFamily adminMembership, CancellationToken ct);

    Task UpdateGroupDetailsAsync(Guid groupId, string name, string? description, CancellationToken ct);

    Task AddFamilyToGroupAsync(GroupFamily membership, CancellationToken ct);

    Task RemoveFamilyFromGroupAsync(Guid groupFamilyId, CancellationToken ct);

    Task UpdateInviteCodeAsync(Guid groupId, string newCode, CancellationToken ct);
}

/// <summary>A family's membership row in a group, used by the leave-guard logic.</summary>
public sealed record GroupMembershipInfo(Guid GroupFamilyId, MemberRole Role);
