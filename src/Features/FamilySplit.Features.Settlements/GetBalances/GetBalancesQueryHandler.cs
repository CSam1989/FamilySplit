using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.GetBalances;

/// <summary>
/// Query — read-only per-family net balances for an activity (the pre-settlement view; creates no
/// rows). Pure data access: lookup + membership guard, then the expense/participant projections feed
/// <see cref="BalanceCalculator"/>. The balance SQL is intentionally duplicated with the write-side
/// gateway (ADR-001 allows it — both are data access). Testcontainers-tested.
/// </summary>
public sealed class GetBalancesQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<GetBalancesQueryHandler> _logger;

    public GetBalancesQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<GetBalancesQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<List<FamilyBalanceDto>> HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Getting balances for activity {ActivityId} requested by user {UserId}", activityId, callerId);

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct);
        if (activity is null)
        {
            _logger.LogDebug("Activity {ActivityId} not found for balances requested by user {UserId}", activityId, callerId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var allIds = await GetActivityAndSubIdsAsync(activityId, ct);
        var currency = await GetCurrencyAsync(allIds, ct);

        var expenses = await (
            from e in _db.Expenses.AsNoTracking()
            from fm in _db.FamilyMembers
            where allIds.Contains(e.ActivityId)
                && fm.UserId != null
                && fm.UserId == e.PaidByUserId
            select new BalanceCalculator.ExpenseData(fm.FamilyId, e.TotalAmount)
        ).ToListAsync(ct);

        var participants = await (
            from ep in _db.ExpenseParticipants.AsNoTracking()
            join e in _db.Expenses on ep.ExpenseId equals e.Id
            join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
            where allIds.Contains(e.ActivityId) && !ep.IsExcluded
            select new BalanceCalculator.ParticipantData(fm.FamilyId, ep.CalculatedAmount)
        ).ToListAsync(ct);

        var balances = BalanceCalculator.Compute(expenses, participants);

        _logger.LogDebug("Computed balances for {FamilyCount} families on activity {ActivityId}", balances.Count, activityId);

        var familyIds = balances.Keys.ToList();
        var familyNames = await _db.Families
            .AsNoTracking()
            .Where(f => familyIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        return balances
            .Select(kv => new FamilyBalanceDto(
                kv.Key,
                familyNames.GetValueOrDefault(kv.Key, "Unknown"),
                Math.Round(kv.Value, 2, MidpointRounding.AwayFromZero),
                currency))
            .OrderByDescending(b => b.Balance)
            .ToList();
    }

    private async Task<string> GetCurrencyAsync(List<Guid> allActivityIds, CancellationToken ct)
    {
        var currency = await _db.Expenses
            .AsNoTracking()
            .Where(e => allActivityIds.Contains(e.ActivityId))
            .GroupBy(e => e.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        return currency ?? "EUR";
    }

    private async Task<List<Guid>> GetActivityAndSubIdsAsync(Guid activityId, CancellationToken ct)
    {
        var subIds = await _db.Activities
            .AsNoTracking()
            .Where(a => a.ParentActivityId == activityId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        subIds.Add(activityId);
        return subIds;
    }
}
