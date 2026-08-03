using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Notifications;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.Data;

/// <summary>
/// The Settlements write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain records; writes accept
/// the command handler's computed state and persist it together with the audit row (queued on the
/// same context, flushed in one <c>SaveChangesAsync</c> — atomic) and dispatch the post-save family
/// notification on the same request scope. Verified with Testcontainers through the settlement
/// endpoints.
/// </summary>
internal sealed class SettlementData : ISettlementData
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly INotificationService _notifications;
    private readonly ILogger<SettlementData> _logger;

    public SettlementData(
        AppDbContext db,
        AuditService audit,
        INotificationService notifications,
        ILogger<SettlementData> logger)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
        _logger = logger;
    }

    // ── reads ─────────────────────────────────────────────────────────────────────

    public async Task<ActivityForSettlement?> GetActivityAsync(Guid activityId, CancellationToken ct) =>
        await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == activityId)
            .Select(a => new ActivityForSettlement(a.GroupId, a.Status, a.ParentActivityId))
            .FirstOrDefaultAsync(ct);

    public async Task<int> CountSettlementsAsync(Guid activityId, CancellationToken ct) =>
        await _db.Settlements.AsNoTracking().CountAsync(s => s.ActivityId == activityId, ct);

    public async Task<string> GetActivityCurrencyAsync(Guid activityId, CancellationToken ct)
    {
        var allIds = await GetActivityAndSubIdsAsync(activityId, ct);

        var currency = await _db.Expenses
            .AsNoTracking()
            .Where(e => allIds.Contains(e.ActivityId))
            .GroupBy(e => e.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        return currency ?? "EUR";
    }

    public async Task<SettlementBalanceInputs> GetBalanceInputsAsync(Guid activityId, CancellationToken ct)
    {
        var allIds = await GetActivityAndSubIdsAsync(activityId, ct);

        // NOTE: the payer join is deliberately NOT filtered by fm.IsActive — the family
        // that fronted the money is the same regardless of whether that member has since
        // been deactivated. Filtering here would drop the credit while debits remain,
        // breaking the zero-sum balance invariant.
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

        return new SettlementBalanceInputs(expenses, participants);
    }

    public async Task<SettlementForConfirm?> GetSettlementForConfirmAsync(Guid settlementId, CancellationToken ct) =>
        await (
            from s in _db.Settlements.AsNoTracking()
            join a in _db.Activities on s.ActivityId equals a.Id
            where s.Id == settlementId
            select new SettlementForConfirm(
                s.Id, s.ActivityId, a.GroupId, s.PayerFamilyId, s.ReceiverFamilyId, s.Amount, s.Currency, s.Status)
        ).FirstOrDefaultAsync(ct);

    public async Task<bool> AreOtherSettlementsCompletedAsync(Guid activityId, Guid excludeSettlementId, CancellationToken ct) =>
        await _db.Settlements
            .AsNoTracking()
            .Where(s => s.ActivityId == activityId && s.Id != excludeSettlementId)
            .AllAsync(s => s.Status == SettlementStatus.Completed, ct);

    // ── writes ──────────────────────────────────────────────────────────────────

    public async Task MarkActivitySettledAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await _db.Activities.FindAsync([activityId], ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        activity.Status = ActivityStatus.Settled;
        await _db.SaveChangesAsync(ct);
    }

    public async Task GenerateSettlementsAsync(
        Guid activityId, IReadOnlyList<Settlement> settlements, IReadOnlyList<AuditEntry> audits, CancellationToken ct)
    {
        _db.Settlements.AddRange(settlements);

        foreach (var audit in audits)
            _audit.Queue(audit);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent generation won the race and inserted rows first; the unique
            // index on (activity, payer, receiver) rejected ours. If the winner's rows
            // exist, detach our duplicates and treat the operation as the idempotent
            // success it is — the client re-queries the list either way.
            var raced = await _db.Settlements.AsNoTracking().AnyAsync(s => s.ActivityId == activityId, ct);
            if (!raced)
                throw;

            foreach (var s in settlements)
                _db.Entry(s).State = EntityState.Detached;

            _logger.LogInformation(
                "Concurrent settlement generation detected for activity {ActivityId} — keeping the rows persisted by the other request",
                activityId);
        }
    }

    public async Task ConfirmSentAsync(
        Guid settlementId, ApprovalStep step, AuditEntry audit, SettlementNotification notify, CancellationToken ct)
    {
        var settlement = await _db.Settlements.FindAsync([settlementId], ct)
            ?? throw ValidationErrors.NotFound("Settlement not found.");

        settlement.Status = SettlementStatus.PayerSent;
        _db.ApprovalSteps.Add(step);
        _audit.Queue(audit);

        await _db.SaveChangesAsync(ct);

        // Awaited (not fire-and-forget): the notification path uses the same request-scoped
        // DbContext, which must not be accessed concurrently or after the scope is disposed.
        await _notifications.NotifyFamilyAsync(notify.TargetFamilyId, notify.Title, notify.Message, notify.Url, ct);
    }

    public async Task ConfirmReceivedAsync(
        Guid settlementId,
        Guid activityId,
        DateTimeOffset completedAt,
        ApprovalStep step,
        AuditEntry audit,
        bool markActivitySettled,
        SettlementNotification notify,
        CancellationToken ct)
    {
        var settlement = await _db.Settlements.FindAsync([settlementId], ct)
            ?? throw ValidationErrors.NotFound("Settlement not found.");

        settlement.Status = SettlementStatus.Completed;
        settlement.CompletedAt = completedAt;
        _db.ApprovalSteps.Add(step);
        _audit.Queue(audit);

        // The status flip and the activity transition commit in a single atomic save.
        if (markActivitySettled)
        {
            var activity = await _db.Activities.FindAsync([activityId], ct)
                ?? throw ValidationErrors.NotFound("Activity not found.");
            activity.Status = ActivityStatus.Settled;
        }

        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyFamilyAsync(notify.TargetFamilyId, notify.Title, notify.Message, notify.Url, ct);
    }

    // ── private helpers ───────────────────────────────────────────────────────────

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
