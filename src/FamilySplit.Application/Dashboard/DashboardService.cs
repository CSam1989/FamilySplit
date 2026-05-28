using FamilySplit.Application.Dashboard.Dtos;
using FamilySplit.Application.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Application.Dashboard;

/// <summary>
/// Returns per-group statistics for the authenticated user's dashboard.
/// All queries use explicit joins (no navigation-property access) to avoid
/// EF Core 10 NavigationExpandingExpressionVisitor cycle errors.
/// </summary>
public class DashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<List<DashboardGroupStatDto>> GetStatsAsync(Guid callerId, CancellationToken ct = default)
    {
        // ── 1. Resolve caller's family ────────────────────────────────────────
        var callerFamilyId = await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("Caller has no active family membership.");

        // ── 2. Groups the caller's family belongs to ──────────────────────────
        var groupInfos = await (
            from gf in _db.GroupFamilies
            join g in _db.Groups on gf.GroupId equals g.Id
            where gf.FamilyId == callerFamilyId
            orderby g.Name
            select new { g.Id, g.Name }
        ).ToListAsync(ct);

        if (groupInfos.Count == 0) return [];

        var groupIds = groupInfos.Select(g => g.Id).ToList();

        // ── 3. Top-level activities (flat scalars, no navigation props) ───────
        var activityRows = await _db.Activities
            .Where(a => groupIds.Contains(a.GroupId) && a.ParentActivityId == null)
            .Select(a => new { a.GroupId, a.Id, a.Name, a.Status, a.CreatedAt })
            .ToListAsync(ct);

        var activityIds = activityRows.Select(a => a.Id).ToList();

        // ── 4. Expense totals per group (all activities, historical view) ─────
        var expensesByGroup = new Dictionary<Guid, (decimal Total, string Currency)>();

        if (activityIds.Count > 0)
        {
            var rawExpenses = await (
                from e in _db.Expenses
                join a in _db.Activities on e.ActivityId equals a.Id
                where activityIds.Contains(e.ActivityId)
                select new { a.GroupId, e.TotalAmount, e.Currency }
            ).ToListAsync(ct);

            expensesByGroup = rawExpenses
                .GroupBy(r => r.GroupId)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        Total: g.Sum(r => r.TotalAmount),
                        Currency: g.GroupBy(r => r.Currency)
                                   .OrderByDescending(c => c.Count())
                                   .Select(c => c.Key)
                                   .FirstOrDefault() ?? "EUR"));
        }

        // ── 5. My family's total share across ALL activities (matches TotalGroupSpend scope) ──
        var shareByGroup = new Dictionary<Guid, decimal>();

        if (activityIds.Count > 0)
        {
            var rawShare = await (
                from ep in _db.ExpenseParticipants
                join e in _db.Expenses on ep.ExpenseId equals e.Id
                join a in _db.Activities on e.ActivityId equals a.Id
                join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
                where activityIds.Contains(e.ActivityId)
                   && fm.FamilyId == callerFamilyId
                   && !ep.IsExcluded
                select new { a.GroupId, ep.CalculatedAmount }
            ).ToListAsync(ct);

            shareByGroup = rawShare
                .GroupBy(r => r.GroupId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CalculatedAmount));
        }

        // ── 7. Active spend (Open + Closed only) — same scope as net balance ────
        var balanceActivityIds = activityRows
            .Where(a => a.Status is ActivityStatus.Open or ActivityStatus.Closed)
            .Select(a => a.Id)
            .ToList();

        var activeExpensesByGroup = new Dictionary<Guid, decimal>();
        var activeShareByGroup = new Dictionary<Guid, decimal>();

        if (balanceActivityIds.Count > 0)
        {
            var rawActiveExpenses = await (
                from e in _db.Expenses
                join a in _db.Activities on e.ActivityId equals a.Id
                where balanceActivityIds.Contains(e.ActivityId)
                select new { a.GroupId, e.TotalAmount }
            ).ToListAsync(ct);

            activeExpensesByGroup = rawActiveExpenses
                .GroupBy(r => r.GroupId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));

            var rawActiveShare = await (
                from ep in _db.ExpenseParticipants
                join e in _db.Expenses on ep.ExpenseId equals e.Id
                join a in _db.Activities on e.ActivityId equals a.Id
                join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
                where balanceActivityIds.Contains(e.ActivityId)
                   && fm.FamilyId == callerFamilyId
                   && !ep.IsExcluded
                select new { a.GroupId, ep.CalculatedAmount }
            ).ToListAsync(ct);

            activeShareByGroup = rawActiveShare
                .GroupBy(r => r.GroupId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CalculatedAmount));
        }

        // ── 9. Net balance across Open + Closed activities only ───────────────
        //   Balance = what my family PAID  minus  what my family OWES.
        //   This mirrors BalanceCalculator logic but scoped to the caller's family.
        var paidByGroup = new Dictionary<Guid, decimal>();
        var owedByGroup = new Dictionary<Guid, decimal>();

        if (balanceActivityIds.Count > 0)
        {
            // What my family paid: expenses whose PaidByUser is a member of my family.
            // Cross-join pattern (fm.UserId == e.PaidByUserId) matching
            // SettlementService.LoadExpenseDataAsync — approved in CLAUDE.md.
            var rawPaid = await (
                from e in _db.Expenses
                from fm in _db.FamilyMembers
                join a in _db.Activities on e.ActivityId equals a.Id
                where balanceActivityIds.Contains(e.ActivityId)
                   && fm.UserId != null
                   && fm.UserId == e.PaidByUserId
                   && fm.FamilyId == callerFamilyId
                   && fm.IsActive
                select new { a.GroupId, e.TotalAmount }
            ).ToListAsync(ct);

            paidByGroup = rawPaid
                .GroupBy(r => r.GroupId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));

            // What my family owes: each member's CalculatedAmount across those activities.
            var rawOwed = await (
                from ep in _db.ExpenseParticipants
                join e in _db.Expenses on ep.ExpenseId equals e.Id
                join a in _db.Activities on e.ActivityId equals a.Id
                join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
                where balanceActivityIds.Contains(e.ActivityId)
                   && fm.FamilyId == callerFamilyId
                   && !ep.IsExcluded
                select new { a.GroupId, ep.CalculatedAmount }
            ).ToListAsync(ct);

            owedByGroup = rawOwed
                .GroupBy(r => r.GroupId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CalculatedAmount));
        }

        // ── 10. Pending settlements (require my family's action) ─────────────
        //   • Proposed  + PayerFamily == mine  → I need to confirm sending.
        //   • PayerSent + ReceiverFamily == mine → I need to confirm receiving.
        var pendingCountByGroup = new Dictionary<Guid, int>();

        if (activityIds.Count > 0)
        {
            var rawPending = await (
                from s in _db.Settlements
                join a in _db.Activities on s.ActivityId equals a.Id
                where activityIds.Contains(s.ActivityId)
                   && (
                       (s.PayerFamilyId == callerFamilyId && s.Status == SettlementStatus.Proposed)
                    || (s.ReceiverFamilyId == callerFamilyId && s.Status == SettlementStatus.PayerSent)
                   )
                select a.GroupId
            ).ToListAsync(ct);

            pendingCountByGroup = rawPending
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        // ── 11. Aggregate in memory and assemble DTOs ─────────────────────────
        var activityCountByGroup = activityRows
            .GroupBy(a => a.GroupId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Count(),
                    Open = g.Count(a => a.Status == ActivityStatus.Open),
                    Closed = g.Count(a => a.Status == ActivityStatus.Closed),
                    Settled = g.Count(a => a.Status == ActivityStatus.Settled),
                    Latest = g.OrderByDescending(a => a.CreatedAt).First(),
                });

        return groupInfos.Select(g =>
        {
            var counts = activityCountByGroup.GetValueOrDefault(g.Id);
            expensesByGroup.TryGetValue(g.Id, out var spend);
            shareByGroup.TryGetValue(g.Id, out var share);
            activeExpensesByGroup.TryGetValue(g.Id, out var activeSpend);
            activeShareByGroup.TryGetValue(g.Id, out var activeShare);
            paidByGroup.TryGetValue(g.Id, out var paid);
            owedByGroup.TryGetValue(g.Id, out var owed);
            pendingCountByGroup.TryGetValue(g.Id, out var pending);

            var balance = Math.Round(paid - owed, 2, MidpointRounding.AwayFromZero);

            return new DashboardGroupStatDto(
                g.Id,
                g.Name,
                counts?.Total ?? 0,
                counts?.Open ?? 0,
                counts?.Closed ?? 0,
                counts?.Settled ?? 0,
                Math.Round(spend.Total, 2, MidpointRounding.AwayFromZero),
                Math.Round(share, 2, MidpointRounding.AwayFromZero),
                Math.Round(activeSpend, 2, MidpointRounding.AwayFromZero),
                Math.Round(activeShare, 2, MidpointRounding.AwayFromZero),
                spend.Currency ?? "EUR",
                balance,
                pending,
                counts?.Latest.Name,
                counts?.Latest.Status.ToString());
        }).ToList();
    }
}
