using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Shared;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.Generate;

/// <summary>
/// Command (business logic) — generates the optimised settlement graph for a closed activity:
/// computes per-family balances (<see cref="BalanceCalculator"/>), reduces them to the minimum set of
/// transfers (<see cref="SettlementOptimiser"/>), and persists one <c>Settlement</c> per transfer.
/// Idempotent — a second call (or a Settled / already-generated activity) is a no-op; an all-even
/// activity is marked Settled immediately. Holds no EF; all data access goes through
/// <see cref="ISettlementData"/> (ADR-001). Returns nothing (204) — the client re-queries the list.
/// </summary>
public sealed class GenerateSettlementsCommandHandler
{
    private readonly ISettlementData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<GenerateSettlementsCommandHandler> _logger;

    public GenerateSettlementsCommandHandler(
        ISettlementData data,
        IGroupMembershipGuard guard,
        ILogger<GenerateSettlementsCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid activityId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Generating settlements for activity {ActivityId} requested by user {UserId}", activityId, callerId);

        var activity = await _data.GetActivityAsync(activityId, ct);
        if (activity is null)
        {
            _logger.LogDebug("Activity {ActivityId} not found for settlement generation by user {UserId}", activityId, callerId);
            throw ValidationErrors.NotFound("Activity not found.");
        }

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        if (activity.Status == ActivityStatus.AbsorbedByParent)
        {
            _logger.LogDebug("Cannot generate settlements for absorbed sub-activity {ActivityId}", activityId);
            throw ValidationErrors.Field("Status", "Cannot settle a sub-activity that was absorbed by its parent.");
        }

        if (activity.ParentActivityId is not null)
        {
            _logger.LogDebug("Cannot generate settlements independently for sub-activity {ActivityId}", activityId);
            throw ValidationErrors.Field("Status", "Sub-activities cannot be settled independently. Generate settlements from the parent activity instead.");
        }

        // Idempotency: if settlements already exist, do nothing — the client re-queries the list.
        // This also absorbs a concurrent second request (e.g. double-dispatch).
        if (await _data.CountSettlementsAsync(activityId, ct) > 0)
        {
            _logger.LogDebug("Settlements already exist for activity {ActivityId} — no-op", activityId);
            return;
        }

        // A Settled activity with no settlement rows was an all-balances-even settle —
        // treat a repeat call as an idempotent no-op rather than an error.
        if (activity.Status == ActivityStatus.Settled)
        {
            _logger.LogDebug("Activity {ActivityId} already Settled with no settlement rows — idempotent no-op", activityId);
            return;
        }

        if (activity.Status == ActivityStatus.Open)
        {
            _logger.LogDebug("Cannot generate settlements for activity {ActivityId} — status is Open, must be closed first", activityId);
            throw ValidationErrors.Field("Status", "Activity must be closed before generating settlements.");
        }

        var currency = await _data.GetActivityCurrencyAsync(activityId, ct);
        var inputs = await _data.GetBalanceInputsAsync(activityId, ct);
        var balances = BalanceCalculator.Compute(inputs.Expenses, inputs.Participants);
        var transfers = SettlementOptimiser.Optimise(balances);

        if (transfers.Count == 0)
        {
            _logger.LogInformation("All balances are zero for activity {ActivityId} — marking as Settled immediately", activityId);
            await _data.MarkActivitySettledAsync(activityId, ct);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var settlements = transfers.Select(t => new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            PayerFamilyId = t.PayerFamilyId,
            ReceiverFamilyId = t.ReceiverFamilyId,
            Amount = t.Amount,
            Currency = currency,
            Status = SettlementStatus.Proposed,
            ProposedAt = now,
        }).ToList();

        var audits = settlements.Select(s => new AuditEntry(callerId, "Settlement", s.Id, "Generated", new
        {
            activityId,
            payerFamilyId = s.PayerFamilyId,
            receiverFamilyId = s.ReceiverFamilyId,
            amount = s.Amount,
            currency = s.Currency,
        })).ToList();

        await _data.GenerateSettlementsAsync(activityId, settlements, audits, ct);

        _logger.LogInformation(
            "Generated {Count} settlement(s) for activity {ActivityId} by user {UserId} — total transfers: {Total} {Currency}",
            settlements.Count, activityId, callerId, settlements.Sum(s => s.Amount), currency);
    }
}
