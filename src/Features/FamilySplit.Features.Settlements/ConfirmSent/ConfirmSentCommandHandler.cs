using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Shared;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.ConfirmSent;

/// <summary>
/// Command (business logic) — the payer family marks a Proposed settlement as PayerSent. Enforces the
/// payer-family membership and the state-machine transition, records an approval step + audit, then the
/// gateway notifies the receiver family. Holds no EF; all data access goes through
/// <see cref="ISettlementData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class ConfirmSentCommandHandler
{
    private readonly ISettlementData _data;
    private readonly IGroupMembershipGuard _guard;
    private readonly ILogger<ConfirmSentCommandHandler> _logger;

    public ConfirmSentCommandHandler(
        ISettlementData data,
        IGroupMembershipGuard guard,
        ILogger<ConfirmSentCommandHandler> logger)
    {
        _data = data;
        _guard = guard;
        _logger = logger;
    }

    public async Task HandleAsync(Guid settlementId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("ConfirmSent for settlement {SettlementId} by user {UserId}", settlementId, callerId);

        var settlement = await _data.GetSettlementForConfirmAsync(settlementId, ct)
            ?? throw ValidationErrors.NotFound("Settlement not found.");

        await _guard.RequireGroupMemberAsync(settlement.GroupId, callerId, ct);

        var callerFamilyId = await _guard.GetCallerFamilyIdAsync(callerId, ct);
        if (callerFamilyId != settlement.PayerFamilyId)
            throw new ForbiddenException("Only a member of the paying family can confirm payment sent.");

        if (!SettlementStateMachine.CanConfirmSent(settlement.Status))
            throw ValidationErrors.Field("Status", $"Settlement is {settlement.Status}; expected Proposed.");

        var now = DateTimeOffset.UtcNow;

        var step = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            SettlementId = settlementId,
            ApproverId = callerId,
            StepType = StepType.PayerSent,
            Status = StepStatus.Done,
            ActionedAt = now,
            CreatedAt = now,
        };

        var audit = new AuditEntry(callerId, "Settlement", settlementId, "ConfirmSent", new
        {
            payerFamilyId = settlement.PayerFamilyId,
            receiverFamilyId = settlement.ReceiverFamilyId,
            amount = settlement.Amount,
            currency = settlement.Currency,
        });

        var notify = new SettlementNotification(
            settlement.ReceiverFamilyId,
            "Payment incoming",
            $"{settlement.Amount:F2} {settlement.Currency} is on its way to you.",
            $"/groups/{settlement.GroupId}/activities/{settlement.ActivityId}");

        await _data.ConfirmSentAsync(settlementId, step, audit, notify, ct);

        _logger.LogInformation(
            "Settlement {SettlementId} marked as sent by user {UserId} (payer family {PayerFamilyId}) — {Amount} {Currency}",
            settlementId, callerId, settlement.PayerFamilyId, settlement.Amount, settlement.Currency);
    }
}
