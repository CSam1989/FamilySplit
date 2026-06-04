using FamilySplit.Application.Activities.Dtos;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly AddParticipantValidator _addParticipantValidator;
    private readonly ParticipantSeeder _seeder;
    private readonly ILogger<ActivityService> _logger;

    public ActivityService(
        AppDbContext db,
        CreateActivityValidator createValidator,
        UpdateActivityValidator updateValidator,
        AddParticipantValidator addParticipantValidator,
        ParticipantSeeder seeder,
        ILogger<ActivityService> logger)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addParticipantValidator = addParticipantValidator;
        _seeder = seeder;
        _logger = logger;
    }

    // ── List activities in a group ────────────────────────────────────────────

    /// <summary>Returns all top-level activities (no parent) for the group.</summary>
    public async Task<List<ActivitySummaryDto>> ListAsync(Guid groupId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("ListAsync called for {GroupId} by {UserId}", groupId, callerId);
        await RequireGroupMemberAsync(groupId, callerId, ct);

        var activities = await _db.Activities
            .AsNoTracking()
            .Where(a => a.GroupId == groupId && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();

        // Aggregate participant counts, sub-activity counts, and expense totals
        // entirely in the database — avoids pulling every Expense row to the API.
        var participantCounts = await _db.ActivityParticipants
            .AsNoTracking()
            .Where(ap => activityIds.Contains(ap.ActivityId))
            .GroupBy(ap => ap.ActivityId)
            .Select(g => new { ActivityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ActivityId, x => x.Count, ct);

        var subActivityCounts = await _db.Activities
            .AsNoTracking()
            .Where(a => a.ParentActivityId != null && activityIds.Contains(a.ParentActivityId.Value))
            .GroupBy(a => a.ParentActivityId!.Value)
            .Select(g => new { ParentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ParentId, x => x.Count, ct);

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
                Count = g.Count(),
                Total = g.Sum(e => e.TotalAmount),
                Currency = g.Min(e => e.Currency)!,
            })
            .ToDictionaryAsync(x => x.ActivityId, ct);

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

    public async Task<ActivityDetailDto> GetDetailAsync(Guid activityId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("GetDetailAsync called for {ActivityId} by {UserId}", activityId, callerId);
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Create top-level activity ─────────────────────────────────────────────

    public async Task<ActivityDetailDto> CreateAsync(Guid groupId, CreateActivityRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("CreateAsync called for {GroupId} by {UserId}", groupId, callerId);
        await _createValidator.ValidateAndThrowAsync(req, ct);
        await RequireGroupMemberAsync(groupId, callerId, ct);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            CreatedByUserId = callerId,
            Status = ActivityStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Activities.Add(activity);
        await _seeder.SeedForActivityAsync(activity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Activity {ActivityId} created in group {GroupId} by {UserId}", activity.Id, groupId, callerId);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Create sub-activity (depth-1 guard) ───────────────────────────────────

    public async Task<ActivityDetailDto> CreateSubActivityAsync(Guid parentActivityId, CreateActivityRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("CreateSubActivityAsync called for parent {ActivityId} by {UserId}", parentActivityId, callerId);
        await _createValidator.ValidateAndThrowAsync(req, ct);

        var parent = await _db.Activities
            .Where(a => a.Id == parentActivityId)
            .Select(a => new { a.Id, a.GroupId, a.ParentActivityId, a.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound();

        // Depth-1 guard: sub-activities may not themselves have a parent.
        if (parent.ParentActivityId is not null)
            throw Throw422("ParentActivityId", "Sub-activities cannot be nested more than one level deep.");

        if (parent.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot add a sub-activity to a closed or settled activity.");

        await RequireGroupMemberAsync(parent.GroupId, callerId, ct);

        var sub = new Activity
        {
            Id = Guid.NewGuid(),
            GroupId = parent.GroupId,
            ParentActivityId = parentActivityId,
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            CreatedByUserId = callerId,
            Status = ActivityStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Activities.Add(sub);
        await _seeder.SeedForSubActivityAsync(sub, parentActivityId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Sub-activity {ActivityId} created under parent {ParentActivityId} by {UserId}", sub.Id, parentActivityId, callerId);

        return await BuildDetailDtoAsync(sub.Id, sub.GroupId, sub.Name,
            sub.Description, sub.Status, sub.ParentActivityId,
            sub.CreatedAt, sub.ClosedAt, ct);
    }

    // ── Update activity ───────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> UpdateAsync(Guid activityId, UpdateActivityRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("UpdateAsync called for {ActivityId} by {UserId}", activityId, callerId);
        await _updateValidator.ValidateAndThrowAsync(req, ct);

        var activity = await _db.Activities.FindAsync([activityId], ct)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Only open activities can be edited.");

        activity.Name = req.Name.Trim();
        activity.Description = req.Description?.Trim();
        activity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Activity {ActivityId} updated by {UserId}", activityId, callerId);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Close activity ────────────────────────────────────────────────────────

    /// <summary>
    /// Closes the activity. Any Open sub-activities are transitioned to
    /// AbsorbedByParent so their costs roll up into the parent.
    /// </summary>
    public async Task<ActivityDetailDto> CloseAsync(Guid activityId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("CloseAsync called for {ActivityId} by {UserId}", activityId, callerId);
        var activity = await _db.Activities.FindAsync([activityId], ct)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (!ActivityCloseGuard.CanClose(activity.Status))
            throw Throw422("Status", "Activity is already closed or settled.");

        if (!ActivityCloseGuard.IsTopLevel(activity.ParentActivityId))
            throw Throw422("Status", "Sub-activities cannot be closed directly. Close the parent activity instead.");

        var now = DateTimeOffset.UtcNow;

        // Absorb any open sub-activities.
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

        _logger.LogInformation("Activity {ActivityId} closed by {UserId}; {Count} sub-activities absorbed", activityId, callerId, openSubs.Count);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Add participant ───────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> AddParticipantAsync(Guid activityId, AddParticipantRequest req, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("AddParticipantAsync called for {ActivityId} by {UserId}", activityId, callerId);
        await _addParticipantValidator.ValidateAndThrowAsync(req, ct);
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot add participants to a closed activity.");

        // Verify member is active and belongs to a family in the group.
        var memberInGroup = await (
            from fm in _db.FamilyMembers
            join gf in _db.GroupFamilies on fm.FamilyId equals gf.FamilyId
            where fm.Id == req.FamilyMemberId && fm.IsActive && gf.GroupId == activity.GroupId
            select fm.Id
        ).AnyAsync(ct);

        if (!memberInGroup)
            throw Throw422("FamilyMemberId", "Member is not part of any family in this group.");

        var alreadyParticipant = await _db.ActivityParticipants
            .AnyAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == req.FamilyMemberId, ct);

        if (alreadyParticipant)
            throw Throw422("FamilyMemberId", "Member is already a participant in this activity.");

        _db.ActivityParticipants.Add(new ActivityParticipant
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            FamilyMemberId = req.FamilyMemberId,
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {FamilyMemberId} added as participant to activity {ActivityId} by {UserId}", req.FamilyMemberId, activityId, callerId);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Remove participant ────────────────────────────────────────────────────

    public async Task<ActivityDetailDto> RemoveParticipantAsync(Guid activityId, Guid familyMemberId, Guid callerId, CancellationToken ct = default)
    {
        _logger.LogDebug("RemoveParticipantAsync called for {ActivityId}, member {FamilyMemberId} by {UserId}", activityId, familyMemberId, callerId);
        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw NotFound();

        await RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status != ActivityStatus.Open)
            throw Throw422("Status", "Cannot remove participants from a closed activity.");

        var participant = await _db.ActivityParticipants
            .FirstOrDefaultAsync(ap => ap.ActivityId == activityId && ap.FamilyMemberId == familyMemberId, ct)
            ?? throw Throw422("FamilyMemberId", "Member is not a participant in this activity.");

        _db.ActivityParticipants.Remove(participant);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("FamilyMember {FamilyMemberId} removed as participant from activity {ActivityId} by {UserId}", familyMemberId, activityId, callerId);

        return await BuildDetailDtoAsync(activity.Id, activity.GroupId, activity.Name,
            activity.Description, activity.Status, activity.ParentActivityId,
            activity.CreatedAt, activity.ClosedAt, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task RequireGroupMemberAsync(Guid groupId, Guid callerId, CancellationToken ct)
    {
        var callerFamilyId = await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException();

        var isMember = await _db.GroupFamilies
            .AnyAsync(gf => gf.GroupId == groupId && gf.FamilyId == callerFamilyId, ct);

        if (!isMember)
            throw new ForbiddenException();
    }

    private async Task<ActivityDetailDto> BuildDetailDtoAsync(
        Guid id, Guid groupId, string name, string? description,
        ActivityStatus status, Guid? parentActivityId,
        DateTimeOffset createdAt, DateTimeOffset? closedAt,
        CancellationToken ct)
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
        ).ToListAsync(ct);

        var participantDtos = participants.Select(p =>
        {
            // Reconstruct a lightweight member shell for WeightCalculator.
            var shell = new FamilyMember
            {
                Id = p.FamilyMemberId,
                DisplayName = p.DisplayName,
                FamilyId = p.FamilyId,
                DateOfBirth = p.DateOfBirth,
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
                .ToListAsync(ct);

            var subIds = rawSubs.Select(a => a.Id).ToList();
            var subParticipantCounts = subIds.Count > 0
                ? await _db.ActivityParticipants
                    .AsNoTracking()
                    .Where(ap => subIds.Contains(ap.ActivityId))
                    .GroupBy(ap => ap.ActivityId)
                    .Select(g => new { ActivityId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ActivityId, x => x.Count, ct)
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
                        Count = g.Count(),
                        Total = g.Sum(e => e.TotalAmount),
                        Currency = g.Min(e => e.Currency)!,
                    })
                    .ToDictionaryAsync(
                        x => x.ActivityId,
                        x => (Count: x.Count, Total: x.Total, Currency: (string?)x.Currency),
                        ct)
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
