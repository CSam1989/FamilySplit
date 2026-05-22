namespace FamilySplit.Application.Core;

/// <summary>
/// Seeds participant rows when an activity, sub-activity, or expense is created.
/// Top-level activity     ← all active FamilyMembers of all GroupMemberships
/// Sub-activity           ← parent activity's ActivityParticipants
/// Expense                ← parent (sub-)activity's ActivityParticipants (is_excluded = false)
///
/// Editing ActivityParticipants does NOT touch existing ExpenseParticipants.
/// Implementation lands in Phase 4.
/// </summary>
public class ParticipantSeeder
{
    // Methods added in Phase 4 (Activities) and Phase 5 (Expenses).
}
