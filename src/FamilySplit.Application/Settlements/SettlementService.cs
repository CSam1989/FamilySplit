using FluentValidation;
using FamilySplit.Application.Audit;
using FamilySplit.Application.Core;
using FamilySplit.Application.Exceptions;
using FamilySplit.Application.Settlements.Dtos;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Settlements;

/// <summary>
/// Phase 6 — Settlements: balance calculation per Family, optimised payment graph,
/// and the ConfirmSent / ConfirmReceived approval flow.
/// Activity transitions to Settled when all its settlements reach Completed.
/// </summary>
public class SettlementService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    private readonly ILogger<SettlementService> _logger;

    public SettlementService(AppDbContext db, AuditService audit, ILogger<SettlementService> logger)
    {
        _db     = db;
        _audit  = audit;
        _logger = logger;
    }

    // ── Get per-family balances (read-only, pre-settlement view) ──────────────

    public async Task<List<FamilyBalanceDto>> GetBalancesAsync(Guid activityId, Guid callerId)
    {
        _logger.LogDebug("Getting balances for activity {ActivityId} requested by user {UserId}",
            activityId, callerId);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        var currency = await GetActivityCurrencyAsync(activityId);
        var (expenses, participants) = await LoadExpenseDataAsync(activityId);
        var balances = BalanceCalculator.Compute(expenses, participants);

        _logger.LogDebug("Computed balances for {FamilyCount} families on activity {ActivityId}",
            balances.Count, activityId);

        // Resolve family names.
        var familyIds = balances.Keys.ToList();
        var familyNames = await _db.Families
            .Where(f => familyIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name);

        return balances
            .Select(kv => new FamilyBalanceDto(
                kv.Key,
                familyNames.GetValueOrDefault(kv.Key, "Unknown"),
                Math.Round(kv.Value, 2, MidpointRounding.AwayFromZero),
                currency))
            .OrderByDescending(b => b.Balance)
            .ToList();
    }

    // ── Generate settlements (calculate + optimise + persist) ─────────────────

    public async Task<List<SettlementSummaryDto>> GenerateAsync(Guid activityId, Guid callerId)
    {
        _logger.LogDebug("Generating settlements for activity {ActivityId} requested by user {UserId}",
            activityId, callerId);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId, a.Status })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        if (activity.Status == ActivityStatus.Open)
            throw Throw422("Status", "Activity must be closed before generating settlements.");

        if (activity.Status == ActivityStatus.Settled)
            throw Throw422("Status", "Activity is already settled.");

        if (activity.Status == ActivityStatus.AbsorbedByParent)
            throw Throw422("Status", "Cannot settle a sub-activity that was absorbed by its parent.");

        var parentId = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => a.ParentActivityId)
            .FirstOrDefaultAsync();
        if (parentId is not null)
            throw Throw422("Status", "Sub-activities cannot be settled independently. Generate settlements from the parent activity instead.");

        // Idempotency: if settlements already exist, return them.
        var existing = await _db.Settlements
            .Where(s => s.ActivityId == activityId)
            .ToListAsync();

        if (existing.Count > 0)
        {
            _logger.LogDebug("Settlements already exist for activity {ActivityId} ({Count} rows) — returning existing",
                activityId, existing.Count);
            return await BuildSummaryListAsync(activityId);
        }

        var currency = await GetActivityCurrencyAsync(activityId);
        var (expenses, participants) = await LoadExpenseDataAsync(activityId);
        var balances = BalanceCalculator.Compute(expenses, participants);
        var transfers = SettlementOptimiser.Optimise(balances);

        if (transfers.Count == 0)
        {
            _logger.LogInformation(
                "All balances are zero for activity {ActivityId} — marking as Settled immediately",
                activityId);

            var activityEntity = await _db.Activities.FindAsync(activityId);
            activityEntity!.Status = ActivityStatus.Settled;
            await _db.SaveChangesAsync();
            return [];
        }

        var now = DateTimeOffset.UtcNow;

        var settlements = transfers.Select(t => new Settlement
        {
            Id               = Guid.NewGuid(),
            ActivityId       = activityId,
            PayerFamilyId    = t.PayerFamilyId,
            ReceiverFamilyId = t.ReceiverFamilyId,
            Amount           = t.Amount,
            Currency         = currency,
            Status           = SettlementStatus.Proposed,
            ProposedAt       = now,
        }).ToList();

        _db.Settlements.AddRange(settlements);

        // Queue one audit entry per generated settlement — all persisted atomically below.
        foreach (var s in settlements)
        {
            _audit.Queue(callerId, "Settlement", s.Id, "Generated", new
            {
                activityId,
                payerFamilyId    = s.PayerFamilyId,
                receiverFamilyId = s.ReceiverFamilyId,
                amount           = s.Amount,
                currency         = s.Currency,
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Generated {Count} settlement(s) for activity {ActivityId} by user {UserId} — total transfers: {Total} {Currency}",
            settlements.Count, activityId, callerId,
            settlements.Sum(s => s.Amount), currency);

        return await BuildSummaryListAsync(activityId);
    }

    // ── List active settlements for a group (all activities) ─────────────────

    public async Task<List<GroupSettlementSummaryDto>> ListForGroupAsync(Guid groupId, Guid callerId)
    {
        _logger.LogDebug("Listing settlements for group {GroupId} requested by user {UserId}", groupId, callerId);

        await RequireGroupMemberAsync(groupId, callerId);

        var activities = await _db.Activities
            .Where(a => a.GroupId == groupId && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync();

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();
        var nameMap     = activities.ToDictionary(a => a.Id, a => a.Name);

        var rows = await (
            from s in _db.Settlements
            join pf in _db.Families on s.PayerFamilyId    equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where activityIds.Contains(s.ActivityId)
               && s.Status != SettlementStatus.Completed
               && s.Status != SettlementStatus.Cancelled
            orderby s.ProposedAt
            select new
            {
                s.Id,
                s.ActivityId,
                s.PayerFamilyId,
                PayerFamilyName    = pf.Name,
                s.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                s.Amount,
                s.Currency,
                s.Status,
                s.ProposedAt,
            }
        ).ToListAsync();

        _logger.LogDebug("Found {Count} pending settlements for group {GroupId}", rows.Count, groupId);

        return rows
            .Select(r => new GroupSettlementSummaryDto(
                r.Id,
                groupId,
                r.ActivityId,
                nameMap.GetValueOrDefault(r.ActivityId, "Unknown"),
                r.PayerFamilyId,
                r.PayerFamilyName,
                r.ReceiverFamilyId,
                r.ReceiverFamilyName,
                Math.Round(r.Amount, 2, MidpointRounding.AwayFromZero),
                r.Currency,
                r.Status,
                r.ProposedAt))
            .OrderBy(r => r.ActivityName)
            .ThenBy(r => r.ProposedAt)
            .ToList();
    }

    // ── List all pending settlements for the caller (across all groups) ───────

    public async Task<List<GroupSettlementSummaryDto>> ListMyPendingAsync(Guid callerId)
    {
        _logger.LogDebug("Listing all pending settlements for user {UserId}", callerId);

        var callerFamilyId = await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync()
            ?? throw new ForbiddenException();

        var groupIds = await _db.GroupFamilies
            .Where(gf => gf.FamilyId == callerFamilyId)
            .Select(gf => gf.GroupId)
            .ToListAsync();

        if (groupIds.Count == 0) return [];

        var activities = await _db.Activities
            .Where(a => groupIds.Contains(a.GroupId) && a.ParentActivityId == null)
            .Select(a => new { a.Id, a.GroupId, a.Name })
            .ToListAsync();

        if (activities.Count == 0) return [];

        var activityIds = activities.Select(a => a.Id).ToList();
        var activityMap = activities.ToDictionary(a => a.Id, a => new { a.GroupId, a.Name });

        var rows = await (
            from s in _db.Settlements
            join pf in _db.Families on s.PayerFamilyId    equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where activityIds.Contains(s.ActivityId)
               && s.Status != SettlementStatus.Completed
               && s.Status != SettlementStatus.Cancelled
               && (s.PayerFamilyId == callerFamilyId || s.ReceiverFamilyId == callerFamilyId)
            orderby s.ProposedAt
            select new
            {
                s.Id,
                s.ActivityId,
                s.PayerFamilyId,
                PayerFamilyName    = pf.Name,
                s.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                s.Amount,
                s.Currency,
                s.Status,
                s.ProposedAt,
            }
        ).ToListAsync();

        _logger.LogDebug("Found {Count} pending settlements for user {UserId} across {GroupCount} group(s)",
            rows.Count, callerId, groupIds.Count);

        return rows
            .Select(r =>
            {
                var act = activityMap[r.ActivityId];
                return new GroupSettlementSummaryDto(
                    r.Id,
                    act.GroupId,
                    r.ActivityId,
                    act.Name,
                    r.PayerFamilyId,
                    r.PayerFamilyName,
                    r.ReceiverFamilyId,
                    r.ReceiverFamilyName,
                    Math.Round(r.Amount, 2, MidpointRounding.AwayFromZero),
                    r.Currency,
                    r.Status,
                    r.ProposedAt);
            })
            .OrderBy(r => r.ActivityName)
            .ThenBy(r => r.ProposedAt)
            .ToList();
    }

    // ── List settlements for an activity ─────────────────────────────────────

    public async Task<List<SettlementSummaryDto>> ListAsync(Guid activityId, Guid callerId)
    {
        _logger.LogDebug("Listing settlements for activity {ActivityId} requested by user {UserId}",
            activityId, callerId);

        var activity = await _db.Activities
            .Where(a => a.Id == activityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        return await BuildSummaryListAsync(activityId);
    }

    // ── Get settlement detail ─────────────────────────────────────────────────

    public async Task<SettlementDetailDto> GetDetailAsync(Guid settlementId, Guid callerId)
    {
        _logger.LogDebug("Getting settlement detail {SettlementId} for user {UserId}", settlementId, callerId);

        var settlement = await _db.Settlements
            .Where(s => s.Id == settlementId)
            .Select(s => new { s.Id, s.ActivityId })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Settlement not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == settlement.ActivityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        return await BuildDetailDtoAsync(settlementId);
    }

    // ── Confirm sent (payer side) ─────────────────────────────────────────────

    public async Task<SettlementDetailDto> ConfirmSentAsync(Guid settlementId, Guid callerId)
    {
        _logger.LogDebug("ConfirmSent for settlement {SettlementId} by user {UserId}", settlementId, callerId);

        var settlement = await _db.Settlements.FindAsync(settlementId)
            ?? throw NotFound("Settlement not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == settlement.ActivityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);
        if (callerFamilyId != settlement.PayerFamilyId)
            throw new ForbiddenException("Only a member of the paying family can confirm payment sent.");

        if (settlement.Status != SettlementStatus.Proposed)
            throw Throw422("Status", $"Settlement is {settlement.Status}; expected Proposed.");

        var now = DateTimeOffset.UtcNow;

        settlement.Status = SettlementStatus.PayerSent;

        _db.ApprovalSteps.Add(new ApprovalStep
        {
            Id           = Guid.NewGuid(),
            SettlementId = settlementId,
            ApproverId   = callerId,
            StepType     = StepType.PayerSent,
            Status       = StepStatus.Done,
            ActionedAt   = now,
            CreatedAt    = now,
        });

        // Queue audit entry — persisted atomically with SaveChangesAsync below.
        _audit.Queue(callerId, "Settlement", settlementId, "ConfirmSent", new
        {
            payerFamilyId    = settlement.PayerFamilyId,
            receiverFamilyId = settlement.ReceiverFamilyId,
            amount           = settlement.Amount,
            currency         = settlement.Currency,
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Settlement {SettlementId} marked as sent by user {UserId} (payer family {PayerFamilyId}) — {Amount} {Currency}",
            settlementId, callerId, settlement.PayerFamilyId, settlement.Amount, settlement.Currency);

        return await BuildDetailDtoAsync(settlementId);
    }

    // ── Confirm received (receiver side) ─────────────────────────────────────

    public async Task<SettlementDetailDto> ConfirmReceivedAsync(Guid settlementId, Guid callerId)
    {
        _logger.LogDebug("ConfirmReceived for settlement {SettlementId} by user {UserId}", settlementId, callerId);

        var settlement = await _db.Settlements.FindAsync(settlementId)
            ?? throw NotFound("Settlement not found.");

        var activity = await _db.Activities
            .Where(a => a.Id == settlement.ActivityId)
            .Select(a => new { a.GroupId, a.Id })
            .FirstOrDefaultAsync()
            ?? throw NotFound("Activity not found.");

        await RequireGroupMemberAsync(activity.GroupId, callerId);

        var callerFamilyId = await GetCallerFamilyIdAsync(callerId);
        if (callerFamilyId != settlement.ReceiverFamilyId)
            throw new ForbiddenException("Only a member of the receiving family can confirm payment received.");

        if (settlement.Status != SettlementStatus.PayerSent)
            throw Throw422("Status", $"Settlement is {settlement.Status}; expected PayerSent.");

        var now = DateTimeOffset.UtcNow;

        settlement.Status      = SettlementStatus.Completed;
        settlement.CompletedAt = now;

        _db.ApprovalSteps.Add(new ApprovalStep
        {
            Id           = Guid.NewGuid(),
            SettlementId = settlementId,
            ApproverId   = callerId,
            StepType     = StepType.ReceiverConfirmed,
            Status       = StepStatus.Done,
            ActionedAt   = now,
            CreatedAt    = now,
        });

        // Queue audit entry — persisted atomically with SaveChangesAsync below.
        _audit.Queue(callerId, "Settlement", settlementId, "ConfirmReceived", new
        {
            payerFamilyId    = settlement.PayerFamilyId,
            receiverFamilyId = settlement.ReceiverFamilyId,
            amount           = settlement.Amount,
            currency         = settlement.Currency,
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Settlement {SettlementId} marked as received by user {UserId} (receiver family {ReceiverFamilyId}) — {Amount} {Currency}",
            settlementId, callerId, settlement.ReceiverFamilyId, settlement.Amount, settlement.Currency);

        // If all settlements for this activity are now Completed, mark activity Settled.
        var allDone = await _db.Settlements
            .Where(s => s.ActivityId == settlement.ActivityId)
            .AllAsync(s => s.Status == SettlementStatus.Completed);

        if (allDone)
        {
            _logger.LogInformation(
                "All settlements for activity {ActivityId} are completed — transitioning activity to Settled",
                settlement.ActivityId);

            var activityEntity = await _db.Activities.FindAsync(settlement.ActivityId);
            activityEntity!.Status = ActivityStatus.Settled;
            await _db.SaveChangesAsync();
        }

        return await BuildDetailDtoAsync(settlementId);
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

    private async Task<Guid> GetCallerFamilyIdAsync(Guid callerId)
    {
        return await _db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync()
            ?? throw new ForbiddenException();
    }

    private async Task<string> GetActivityCurrencyAsync(Guid activityId)
    {
        var allIds = await GetActivityAndSubIdsAsync(activityId);

        var currency = await _db.Expenses
            .Where(e => allIds.Contains(e.ActivityId))
            .GroupBy(e => e.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync();

        return currency ?? "EUR";
    }

    private async Task<(List<BalanceCalculator.ExpenseData>, List<BalanceCalculator.ParticipantData>)>
        LoadExpenseDataAsync(Guid activityId)
    {
        var allIds = await GetActivityAndSubIdsAsync(activityId);

        var expenseData = await (
            from e in _db.Expenses
            from fm in _db.FamilyMembers
            where allIds.Contains(e.ActivityId)
                && fm.UserId != null
                && fm.UserId == e.PaidByUserId
                && fm.IsActive
            select new BalanceCalculator.ExpenseData(fm.FamilyId, e.TotalAmount)
        ).ToListAsync();

        var participantData = await (
            from ep in _db.ExpenseParticipants
            join e in _db.Expenses on ep.ExpenseId equals e.Id
            join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
            where allIds.Contains(e.ActivityId) && !ep.IsExcluded
            select new BalanceCalculator.ParticipantData(fm.FamilyId, ep.CalculatedAmount)
        ).ToListAsync();

        return (expenseData, participantData);
    }

    private async Task<List<Guid>> GetActivityAndSubIdsAsync(Guid activityId)
    {
        var subIds = await _db.Activities
            .Where(a => a.ParentActivityId == activityId)
            .Select(a => a.Id)
            .ToListAsync();

        subIds.Add(activityId);
        return subIds;
    }

    private async Task<List<SettlementSummaryDto>> BuildSummaryListAsync(Guid activityId)
    {
        var rows = await (
            from s in _db.Settlements
            join pf in _db.Families on s.PayerFamilyId equals pf.Id
            join rf in _db.Families on s.ReceiverFamilyId equals rf.Id
            where s.ActivityId == activityId
            orderby s.ProposedAt
            select new
            {
                s.Id,
                s.ActivityId,
                s.PayerFamilyId,
                PayerFamilyName   = pf.Name,
                s.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                s.Amount,
                s.Currency,
                s.Status,
                s.ProposedAt,
                s.CompletedAt,
            }
        ).ToListAsync();

        return rows.Select(r => new SettlementSummaryDto(
            r.Id,
            r.ActivityId,
            r.PayerFamilyId,
            r.PayerFamilyName,
            r.ReceiverFamilyId,
            r.ReceiverFamilyName,
            r.Amount,
            r.Currency,
            r.Status,
            r.ProposedAt,
            r.CompletedAt)).ToList();
    }

    private async Task<SettlementDetailDto> BuildDetailDtoAsync(Guid settlementId)
    {
        var s = await (
            from settlement in _db.Settlements
            join pf in _db.Families on settlement.PayerFamilyId equals pf.Id
            join rf in _db.Families on settlement.ReceiverFamilyId equals rf.Id
            where settlement.Id == settlementId
            select new
            {
                settlement.Id,
                settlement.ActivityId,
                settlement.PayerFamilyId,
                PayerFamilyName   = pf.Name,
                settlement.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                settlement.Amount,
                settlement.Currency,
                settlement.Status,
                settlement.Notes,
                settlement.ProposedAt,
                settlement.CompletedAt,
            }
        ).FirstOrDefaultAsync()
          ?? throw NotFound("Settlement not found.");

        var steps = await (
            from step in _db.ApprovalSteps
            join u in _db.Users on step.ApproverId equals u.Id
            where step.SettlementId == settlementId
            orderby step.CreatedAt
            select new ApprovalStepDto(
                step.Id,
                step.ApproverId,
                u.DisplayName,
                step.StepType,
                step.Status,
                step.ActionedAt,
                step.CreatedAt)
        ).ToListAsync();

        return new SettlementDetailDto(
            s.Id,
            s.ActivityId,
            s.PayerFamilyId,
            s.PayerFamilyName,
            s.ReceiverFamilyId,
            s.ReceiverFamilyName,
            s.Amount,
            s.Currency,
            s.Status,
            s.Notes,
            steps,
            s.ProposedAt,
            s.CompletedAt);
    }

    private static ValidationException NotFound(string message) => new(message);

    private static ValidationException Throw422(string field, string message) =>
        new(new[] { new FluentValidation.Results.ValidationFailure(field, message) });
}
