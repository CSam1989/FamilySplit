using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Shared;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.ConfirmReceived;

/// <summary>
/// Command (business logic) — the receiver family confirms a PayerSent settlement as received,
/// completing it. Enforces the receiver-family membership and the state-machine transition, records an
/// approval step + audit, and — when this was the last outstanding settlement — transitions the
/// activity to Settled (atomically in the gateway). Holds no EF; all data access goes through
/// <see cref="ISettlementData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class ConfirmReceivedCommandHandler
{
    private readonly ISettlementData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<ConfirmReceivedCommandHandler> _logger;

    public ConfirmReceivedCommandHandler(
        ISettlementData data,
        IGroupMembershipGuard guard,
        ILogger<ConfirmReceivedCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid settlementId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("ConfirmReceived for settlement {SettlementId} by user {UserId}", settlementId, callerId);

        var settlement = await _data.GetSettlementForConfirmAsync(settlementId, ct);
        if (settlement is null)
        {
            _logger.LogDebug("Settlement {SettlementId} not found for ConfirmReceived by user {UserId}", settlementId, callerId);
            throw ValidationErrors.NotFound("Settlement not found.");
        }

        await _guard.RequireGroupMemberAsync(settlement.GroupId, callerId, ct);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);
        if (callerFamilyId != settlement.ReceiverFamilyId)
        {
            _logger.LogWarning(
                "User {UserId} outside the receiving family attempted to confirm settlement {SettlementId} received", callerId, settlementId);
            throw new ForbiddenException("Only a member of the receiving family can confirm payment received.");
        }

        if (!SettlementStateMachine.CanConfirmReceived(settlement.Status))
        {
            _logger.LogDebug("Settlement {SettlementId} is {Status}; expected PayerSent for ConfirmReceived", settlementId, settlement.Status);
            throw ValidationErrors.Field("Status", $"Settlement is {settlement.Status}; expected PayerSent.");
        }

        var now = DateTimeOffset.UtcNow;

        var step = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            ApproverId = callerId,
            StepType = StepType.ReceiverConfirmed,
            Status = StepStatus.Done,
            ActionedAt = now,
            CreatedAt = now,
        };

        var audit = new AuditEntry(callerId, "Settlement", settlementId, "ConfirmReceived", new
        {
            payerFamilyId = settlement.PayerFamilyId,
            receiverFamilyId = settlement.ReceiverFamilyId,
            amount = settlement.Amount,
            currency = settlement.Currency,
        });

        // If every OTHER settlement on this activity is already Completed, this one completing
        // means the whole activity is settled. The gateway flips both in one atomic save.
        var markActivitySettled = await _data.AreOtherSettlementsCompletedAsync(settlement.ActivityId, settlementId, ct);

        if (markActivitySettled)
            _logger.LogInformation(
                "All settlements for activity {ActivityId} are completed — transitioning activity to Settled",
                settlement.ActivityId);

        var notify = new SettlementNotification(
            settlement.PayerFamilyId,
            "Payment confirmed",
            $"Your payment of {settlement.Amount:F2} {settlement.Currency} was confirmed as received.",
            $"/groups/{settlement.GroupId}/activities/{settlement.ActivityId}");

        await _data.ConfirmReceivedAsync(settlementId, settlement.ActivityId, now, step, audit, markActivitySettled, notify, ct);

        _logger.LogInformation(
            "Settlement {SettlementId} marked as received by user {UserId} (receiver family {ReceiverFamilyId}) — {Amount} {Currency}",
            settlementId, callerId, settlement.ReceiverFamilyId, settlement.Amount, settlement.Currency);
    }
}
