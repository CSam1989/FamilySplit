using FluentValidation;
using FamilySplit.Application.Activities.Dtos;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Application.Activities;

/// <summary>
/// Phase 4 — Activities: create activity / sub-activity (depth-1 guard),
/// participant management, close flow (parent absorbs open subs).
/// </summary>
public class ActivityService
{
    private readonly AppDbContext _db;
    private readonly CreateActivityValidator _createValidator;
    private readonly UpdateActivityValidator _updateValidator;
    private readonly ParticipantSeeder _seeder;

    public ActivityService(
        AppDbContext db,
        CreateActivityValidator createValidator,
        UpdateActivityValidator updateValidator,
        ParticipantSeeder seeder)
    {
        _db              = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _seeder          = seeder;
    }

    // ── List activities in a group ────────────────────────────────────────────

    /// <summary>Returns all top-level activities (no parent) for the group.</summary>
    public async Task<List<ActivitySummaryDto>> ListAsync(Guid groupId, Guid callerId)
    {
        await RequireGroupMemberAsync(groupId, callerId);

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.GroupId == groupId && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();

        // Aggregate participant counts, sub-activity counts, and expense totals
        // entirely in the database — avoids pulling every Expense row to the API.
        var participantCounts = await _db.ActivityParticipants
            .AsNoTracking()
            .Where(ap => activityIds.Contains(ap.ActivityId))
            .GroupBy(ap => ap.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count);

        var subActivityCounts = await _db.Activities
            .AsNoTracking()
            .Where(a => a.ParentActivityId != null && activityIds.Contains(a.ParentActivityId.Value))
            .GroupBy(a => a.ParentActivityId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count);

        // Sum + count per activity in one query; currency picked via a min() so the
        // database doesn't have to ship per-row data. In practice all expenses on
        // an activity share a currency.
        var expenseAggregates = await _db.Expenses
            .AsNoTracking()
            .Where(e => activityIds.Contains(e.ActivityId))
            .GroupBy(e => e.ActivityId)
            .Select(g => new
            {
                ActivityId = g.Key,
                Count      = g.Count(),
                Total      = g.Sum(e => e.TotalAmount),
                Currency   = g.Min(e => e.Currency)!,
            })
            .ToDictionaryAsync(x => x.ActivityId);

        return activities.Select(a =>
        {
            expenseAggregates.TryGetValue(a.Id, out var agg);
            return new ActivitySummaryDto(
                a.Id,
                a.GroupId,
                a.Name,
                a.Description,
                a.Status,
                a.ParentActivityId,
                participantCounts.GetValueOrDefault(a.Id, 0),
                subActivityCounts.GetValueOrDefault(a.Id, 0),
                a.CreatedAt,
                a.ClosedAt,
                agg?.Count ?? 0,
                agg?.Total ?? 0m,
                agg?.Currency ?? "EUR");
        }).ToList();
    }

    // ── Get activity detail ───────────────────────────────────────────────────

    public async Task<ActivityDetailDto> GetDetailAsync(Guid activityId, Guid callerId)
    {
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync()
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Create top-level activity ─────────────────────────────────────────────

    public async Task<ActivityDetailDto> CreateAsync(Guid groupId, CreateActivityRequest req, Guid callerId)
    {
        await _createValidator.ValidateAndThrowAsync(req);
        await RequireGroupMemberAsync(groupId, callerId);

        var activity = new Activity
        {
            Id              = Guid.NewGuid(),
            GroupId         = groupId,
            Name            = req.Name.Trim(),
            Description     = req.Description?.Trim(),
            CreatedByUserId = callerId,
            Status          = ActivityStatus.Open,
            CreatedAt       = DateTimeOffset.UtcNow,
            UpdatedAt       = DateTimeOffset.UtcNow,
        };

        _db.Activities.Add(activity);
        await _seeder.SeedForActivityAsync(activity);
        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Create sub-activity (depth-1 guard) ───────────────────────────────────

    public async Task<ActivityDetailDto> CreateSubActivityAsync(Guid parentActivityId, CreateActivityRequest req, Guid callerId)
    {
        await _createValidator.ValidateAndThrowAsync(req);

        var parent = await _db.Activities
            .Where(a => a.Id == parentActivityId)
            .Select(a => new { a.Id, a.GroupId, a.ParentActivityId, a.Status })
            .FirstOrDefaultAsync()
            ?? throw NotFound();

        // Depth-1 guard: sub-activities may not themselves have a parent.
        if (parent.ParentActivityId is not null)
            throw Throw422("ParentActivityId", "Sub-activities cannot be nested more than one level deep.");

        if (parent.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot add a sub-activity to a closed or settled activity.");

        await RequireGroupMemberAsync(parent.GroupId, callerId);

        var sub = new Activity
        {
            Id               = Guid.NewGuid(),
            GroupId          = parent.GroupId,
            ParentActivityId = parentActivityId,
            Name             = req.Name.Trim(),
            Description      = req.Description?.Trim(),
            CreatedByUserId  = callerId,
            Status           = ActivityStatus.Open,
            CreatedAt        = DateTimeOffset.UtcNow,
            UpdatedAt        = DateTimeOffset.UtcNow,
        };

        _db.Activities.Add(sub);
        await _seeder.SeedForSubActivityAsync(sub, parentActivityId);
        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(sub.Id, sub.GroupId, sub.Name,
            sub.Description, sub.Status, sub.ParentActivityId,
            sub.CreatedAt, sub.ClosedAt);
    }

    // ── Update activity ───────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> UpdateAsync(Guid activityId, UpdateActivityRequest req, Guid callerId)
    {
        await _updateValidator.ValidateAndThrowAsync(req);

        var activity = await _db.Activities.FindAsync(activityId)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Only open activities can be edited.");

        activity.Name        = req.Name.Trim();
        activity.Description = req.Description?.Trim();
        activity.UpdatedAt   = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Close activity ────────────────────────────────────────────────────────

    /// <summary>
    /// Closes the activity. Any Open sub-activities are transitioned to
    /// AbsorbedByParent so their costs roll up into the parent.
    /// </summary>
    public async Task<ActivityDetailDto> CloseAsync(Guid activityId, Guid callerId)
    {
        var activity = await _db.Activities.FindAsync(activityId)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Activity is already closed or settled.");

        // Only top-level activities can be closed directly (sub-activities are absorbed by parent close).
        if (activity.ParentActivityId is not null)
            throw Throw422("Status", "Sub-activities cannot be closed directly. Close the parent activity instead.");

        var now = DateTimeOffset.UtcNow;

        // Absorb any open sub-activities.
        var openSubs = await _db.Activities
            .Where(a => a.ParentActivityId == activityId && a.Status == ActivityStatus.Open)
            .ToListAsync();

        foreach (var sub in openSubs)
        {
            sub.Status         = ActivityStatus.AbsorbedByParent;
            sub.ClosedAt       = now;
            sub.ClosedByUserId = callerId;
            sub.UpdatedAt      = now;
        }

        activity.Status        = ActivityStatus.Closed;
        activity.ClosedAt      = now;
        activity.ClosedByUserId = callerId;
        activity.UpdatedAt     = now;

        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Add participant ───────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> AddParticipantAsync(Guid activityId, AddParticipantRequest req, Guid callerId)
    {
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync()
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot add participants to a closed activity.");

        // Verify member is active and belongs to a family in the group.
        var memberInGroup = await (
            from fm in _db.FamilyMembers
            join gf in _db.GroupFamilies on fm.FamilyId equals gf.FamilyId
            where fm.Id == req.FamilyMemberId && fm.IsActive && gf.GroupId == activity.GroupId
            select fm.Id
        ).AnyAsync();

        if (!memberInGroup)
            throw Throw422("FamilyMemberId", "Member is not part of any family in this group.");

        var alreadyParticipant = await _db.ActivityParticipants
            .AnyAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == req.FamilyMemberId);

        if (alreadyParticipant)
            throw Throw422("FamilyMemberId", "Member is already a participant in this activity.");

        _db.ActivityParticipants.Add(new ActivityParticipant
        {
            Id             = Guid.NewGuid(),
            ActivityId     = activityId,
            FamilyMemberId = req.FamilyMemberId,
        });

        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Remove participant ────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> RemoveParticipantAsync(Guid activityId, Guid familyMemberId, Guid callerId)
    {
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync()
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot remove participants from a closed activity.");

        var participant = await _db.ActivityParticipants
            .FirstOrDefaultAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == familyMemberId)
            ?? throw Throw422("FamilyMemberId", "Member is not a participant in this activity.");

        _db.ActivityParticipants.Remove(participant);
        await _db.SaveChangesAsync();

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RequireGroupMemberAsync(Guid groupId, Guid callerId)
    {
        var callerFamilyId = await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync()
            ?? throw new ForbiddenException();

        var isMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId);

        if (!isMember)
            throw new ForbiddenException();
    }

    private async Task<ActivityDetailDto> BuildDetailDtoAsync(
        Guid id, Guid groupId, string name, string? description,
        ActivityStatus status, Guid? parentActivityId,
        DateTimeOffset createdAt, DateTimeOffset? closedAt)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Participants with member + family info via explicit joins.
        var participants = await (
            from ap in _db.ActivityParticipants
            join fm in _db.FamilyMembers on ap.FamilyMemberId equals fm.Id
            join f in _db.Families on fm.FamilyId equals f.Id
            where ap.ActivityId == id
            orderby f.Name, fm.DisplayName
            select new
            {
                ap.Id,
                ap.FamilyMemberId,
                fm.DisplayName,
                fm.FamilyId,
                FamilyName = f.Name,
                fm.DateOfBirth,
                fm.WeightOverride,
            }
        ).ToListAsync();

        var participantDtos = participants.Select(p =>
        {
            // Reconstruct a lightweight member shell for WeightCalculator.
            var shell = new FamilyMember
            {
                Id             = p.FamilyMemberId,
                DisplayName    = p.DisplayName,
                FamilyId       = p.FamilyId,
                DateOfBirth    = p.DateOfBirth,
                WeightOverride = p.WeightOverride,
            };
            return new ActivityParticipantDto(
                p.Id,
                p.FamilyMemberId,
                p.DisplayName,
                p.FamilyId,
                p.FamilyName,
                WeightCalculator.GetWeight(shell, today),
                WeightCalculator.GetTier(shell, today));
        }).ToList();

        // Sub-activities (for top-level activities only).
        List<ActivitySummaryDto> subDtos;

        if (parentActivityId is null)
        {
            var rawSubs = await _db.Activities
                .Where(a => a.ParentActivityId == id)
                .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var subIds = rawSubs.Select(a => a.Id).ToList();
            var subParticipantCounts = subIds.Count > 0
                ? await _db.ActivityParticipants
                    .AsNoTracking()
                    .Where(ap => subIds.Contains(ap.ActivityId))
                    .GroupBy(ap => ap.ActivityId)
                    .Select(g => new { ActivityId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ActivityId, x => x.Count)
                : new Dictionary<Guid, int>();

            // Expense aggregates per sub-activity, computed in the database.
            // Using a value-tuple keeps the result expressible without a class type.
            var subExpenseAggregates = subIds.Count > 0
                ? await _db.Expenses
                    .AsNoTracking()
                    .Where(e => subIds.Contains(e.ActivityId))
                    .GroupBy(e => e.ActivityId)
                    .Select(g => new
                    {
                        ActivityId = g.Key,
                        Count      = g.Count(),
                        Total      = g.Sum(e => e.TotalAmount),
                        Currency   = g.Min(e => e.Currency)!,
                    })
                    .ToDictionaryAsync(
                        x => x.ActivityId,
                        x => (Count: x.Count, Total: x.Total, Currency: (string?)x.Currency))
                : new Dictionary<Guid, (int Count, decimal Total, string? Currency)>();

            subDtos = rawSubs.Select(a =>
            {
                subExpenseAggregates.TryGetValue(a.Id, out var subAgg);
                return new ActivitySummaryDto(
                    a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId,
                    subParticipantCounts.GetValueOrDefault(a.Id, 0),
                    0, // sub-activities cannot have their own sub-activities
                    a.CreatedAt, a.ClosedAt,
                    subAgg.Count,
                    subAgg.Total,
                    subAgg.Currency ?? "EUR");
            }).ToList();
        }
        else
        {
            subDtos = [];
        }

        return new ActivityDetailDto(id, groupId, name, description, status,
            parentActivityId, participantDtos, subDtos, createdAt, closedAt);
    }

    private static ForbiddenException Forbidden() => new();
    private static ValidationException NotFound() => new("Activity not found.");
    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
