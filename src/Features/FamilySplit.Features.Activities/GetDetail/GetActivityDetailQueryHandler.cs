using FamilySplit.Common.Calculations;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Activities.GetDetail;

/// <summary>
/// Query — full detail for an activity the caller's family belongs to (absorbs the old service's
/// <c>BuildDetailDtoAsync</c>). Pure data access: lookup + membership guard, then explicit-join
/// projections (no navigation properties, per the EF Core 10 cycle-detection workaround) and a
/// current-weight snapshot for each participant. Testcontainers-tested.
/// </summary>
public sealed class GetActivityDetailQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<GetActivityDetailQueryHandler> _logger;

    public GetActivityDetailQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<GetActivityDetailQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<ActivityDetailDto> HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Fetching activity {ActivityId} for user {UserId}", activityId, callerId);

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.Id, a.GroupId, a.Name, a.Description, a.Status, a.ParentActivityId, a.CreatedAt, a.ClosedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Participants with member + family info via explicit joins.
        var participants = await (
            from ap in _db.ActivityParticipants.AsNoTracking()
            join fm in _db.FamilyMembers on ap.FamilyMemberId equals fm.Id
            join f in _db.Families on fm.FamilyId equals f.Id
            where ap.ActivityId == activity.Id
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

        if (activity.ParentActivityId is null)
        {
            var rawSubs = await _db.Activities
                .AsNoTracking()
                .Where(a => a.ParentActivityId == activity.Id)
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

        return new ActivityDetailDto(
            activity.Id, activity.GroupId, activity.Name, activity.Description, activity.Status,
            activity.ParentActivityId, participantDtos, subDtos, activity.CreatedAt, activity.ClosedAt);
    }
}
