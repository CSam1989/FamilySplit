using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Application.Core;

/// <summary>
/// Seeds participant rows when an activity, sub-activity, or expense is created.
///
/// Top-level activity  → all active FamilyMembers of all families in the group.
/// Sub-activity        → the parent activity's existing ActivityParticipants.
/// Expense             → parent (sub-)activity's ActivityParticipants (Phase 5).
///
/// Editing ActivityParticipants does NOT touch existing ExpenseParticipants.
/// </summary>
public class ParticipantSeeder
{
    private readonly AppDbContext _db;

    public ParticipantSeeder(AppDbContext db) => _db = db;

    /// <summary>
    /// Seeds participants for a new top-level activity from all active members
    /// of every family that belongs to the group.
    /// </summary>
    public async Task SeedForActivityAsync(Activity activity)
    {
        // Explicit join to avoid EF navigation cycle issues.
        var memberIds = await (
            from gf in _db.GroupFamilies
            join fm in _db.FamilyMembers on gf.FamilyId equals fm.FamilyId
            where gf.GroupId == activity.GroupId && fm.IsActive
            select fm.Id
        ).Distinct().ToListAsync();

        foreach (var memberId in memberIds)
        {
            _db.ActivityParticipants.Add(new ActivityParticipant
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                FamilyMemberId = memberId,
            });
        }
    }

    /// <summary>
    /// Seeds participants for a new sub-activity from the parent activity's
    /// existing participant list.
    /// </summary>
    public async Task SeedForSubActivityAsync(Activity subActivity, Guid parentActivityId)
    {
        var parentMemberIds = await _db.ActivityParticipants
            .Where(ap => ap.ActivityId == parentActivityId)
            .Select(ap => ap.FamilyMemberId)
            .ToListAsync();

        foreach (var memberId in parentMemberIds)
        {
            _db.ActivityParticipants.Add(new ActivityParticipant
            {
                Id = Guid.NewGuid(),
                ActivityId = subActivity.Id,
                FamilyMemberId = memberId,
            });
        }
    }
}
