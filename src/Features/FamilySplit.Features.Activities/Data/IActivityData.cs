using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Activities.Data;

/// <summary>
/// The Activities slice data-access seam (ADR-001). All EF/<c>AppDbContext</c> access for the write
/// side lives behind this interface so the command handlers (business logic) hold no EF and are
/// unit-tested by mocking it. Reads return plain values/records — never tracked entities. Writes
/// accept the command handler's computed state and persist it. Activity mutations are non-financial,
/// so there is no audit flush here (CLAUDE.md). The implementation is Testcontainers-tested.
/// </summary>
public interface IActivityData
{
    // ── reads (plain values/records, no tracking leaks) ───────────────────────────

    /// <summary>The activity's core fields (group, status, parent), or null if it does not exist.</summary>
    Task<ActivityCore?> GetActivityCoreAsync(Guid activityId, CancellationToken ct);

    /// <summary>The ids of every active FamilyMember belonging to a family in the group.</summary>
    Task<IReadOnlyList<Guid>> GetActiveGroupMemberIdsAsync(Guid groupId, CancellationToken ct);

    /// <summary>The FamilyMember ids that already participate in the given activity.</summary>
    Task<IReadOnlyList<Guid>> GetActivityParticipantMemberIdsAsync(Guid activityId, CancellationToken ct);

    /// <summary>True if the member is active and belongs to a family that is part of the group.</summary>
    Task<bool> IsMemberInGroupAsync(Guid groupId, Guid familyMemberId, CancellationToken ct);

    /// <summary>True if the member is already a participant in the activity.</summary>
    Task<bool> IsParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct);

    // ── writes (accept computed state, persist + SaveChanges) ─────────────────────

    Task PersistNewActivityAsync(Activity activity, IReadOnlyList<ActivityParticipant> participants, CancellationToken ct);

    Task UpdateActivityDetailsAsync(Guid activityId, string name, string? description, CancellationToken ct);

    /// <summary>
    /// Closes the activity and transitions any Open sub-activities to <c>AbsorbedByParent</c>.
    /// Returns the number of sub-activities that were absorbed (for logging).
    /// </summary>
    Task<int> CloseActivityAsync(Guid activityId, Guid callerId, CancellationToken ct);

    Task AddParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct);

    Task RemoveParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct);
}

/// <summary>The core fields of an activity needed by the command guards.</summary>
public sealed record ActivityCore(Guid Id, Guid GroupId, ActivityStatus Status, Guid? ParentActivityId);
