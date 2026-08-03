using FamilySplit.Domain.Entities;

namespace FamilySplit.Features.Activities.Shared;

/// <summary>
/// Pure builder for <see cref="ActivityParticipant"/> rows (ADR-001). The DB reads that decide
/// <em>which</em> members participate live in <c>IActivityData</c>:
/// <list type="bullet">
///   <item>Top-level activity → all active FamilyMembers of all families in the group.</item>
///   <item>Sub-activity → the parent activity's existing participant member ids.</item>
/// </list>
/// This class only maps the resolved member ids → participant entities, so it holds no EF and is
/// unit-tested directly. Editing ActivityParticipants does NOT touch existing ExpenseParticipants.
/// </summary>
public static class ParticipantSeeder
{
    /// <summary>Builds participant rows for a new top-level activity from the group's active member ids.</summary>
    public static List<ActivityParticipant> SeedForActivity(Guid activityId, IEnumerable<Guid> memberIds) =>
        memberIds
            .Select(memberId => new ActivityParticipant
            {
                Id = Guid.NewGuid(),
                ActivityId = activityId,
                FamilyMemberId = memberId,
            })
            .ToList();

    /// <summary>Builds participant rows for a new sub-activity by copying the parent's member ids.</summary>
    public static List<ActivityParticipant> SeedForSubActivity(Guid subActivityId, IEnumerable<Guid> parentMemberIds) =>
        SeedForActivity(subActivityId, parentMemberIds);
}
