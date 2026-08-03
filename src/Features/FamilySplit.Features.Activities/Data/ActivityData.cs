using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Features.Activities.Data;

/// <summary>
/// The Activities write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain values/records; writes
/// accept the command handler's computed state and persist it. Activity mutations are non-financial,
/// so there is no audit flush. Verified with Testcontainers.
/// </summary>
internal sealed class ActivityData : IActivityData
{
    private readonly AppDbContext _db;

    public ActivityData(AppDbContext db) => _db = db;

    public async Task<ActivityCore?> GetActivityCoreAsync(Guid activityId, CancellationToken ct) =>
        await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new ActivityCore(a.Id, a.GroupId, a.Status, a.ParentActivityId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetActiveGroupMemberIdsAsync(Guid groupId, CancellationToken ct) =>
        // Explicit join to avoid EF navigation cycle issues (EF Core 10 workaround).
        await (
            from gf in _db.GroupFamilies
            join fm in _db.FamilyMembers on gf.FamilyId equals fm.FamilyId
            where gf.GroupId == groupId && fm.IsActive
            select fm.Id
        ).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetActivityParticipantMemberIdsAsync(Guid activityId, CancellationToken ct) =>
        await _db.ActivityParticipants
            .AsNoTracking()
            .Where(ap => ap.ActivityId == activityId)
            .Select(ap => ap.FamilyMemberId)
            .ToListAsync(ct);

    public async Task<bool> IsMemberInGroupAsync(Guid groupId, Guid familyMemberId, CancellationToken ct) =>
        await (
            from fm in _db.FamilyMembers
            join gf in _db.GroupFamilies on fm.FamilyId equals gf.FamilyId
            where fm.Id == familyMemberId && fm.IsActive && gf.GroupId == groupId
            select fm.Id
        ).AnyAsync(ct);

    public async Task<bool> IsParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct) =>
        await _db.ActivityParticipants
            .AsNoTracking()
            .AnyAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == familyMemberId, ct);

    public async Task PersistNewActivityAsync(Activity activity, IReadOnlyList<ActivityParticipant> participants, CancellationToken ct)
    {
        _db.Activities.Add(activity);
        _db.ActivityParticipants.AddRange(participants);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateActivityDetailsAsync(Guid activityId, string name, string? description, CancellationToken ct)
    {
        var activity = await _db.Activities.FindAsync([activityId], ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        activity.Name = name;
        activity.Description = description;
        activity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CloseActivityAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        var activity = await _db.Activities.FindAsync([activityId], ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        var now = DateTimeOffset.UtcNow;

        // Absorb any open sub-activities so their costs roll up into the parent.
        var openSubs = await _db.Activities
            .Where(a => a.ParentActivityId == activityId && a.Status == ActivityStatus.Open)
            .ToListAsync(ct);

        foreach (var sub in openSubs)
        {
            sub.Status = ActivityStatus.AbsorbedByParent;
            sub.ClosedAt = now;
            sub.ClosedByUserId = callerId;
            sub.UpdatedAt = now;
        }

        activity.Status = ActivityStatus.Closed;
        activity.ClosedAt = now;
        activity.ClosedByUserId = callerId;
        activity.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return openSubs.Count;
    }

    public async Task AddParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct)
    {
        _db.ActivityParticipants.Add(new ActivityParticipant
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            FamilyMemberId = familyMemberId,
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveParticipantAsync(Guid activityId, Guid familyMemberId, CancellationToken ct)
    {
        var participant = await _db.ActivityParticipants
            .FirstOrDefaultAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == familyMemberId, ct);

        if (participant is null)
            return;

        _db.ActivityParticipants.Remove(participant);
        await _db.SaveChangesAsync(ct);
    }
}
