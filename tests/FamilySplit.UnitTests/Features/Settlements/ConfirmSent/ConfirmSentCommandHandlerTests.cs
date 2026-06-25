using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.ConfirmSent;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.UnitTests.Features.Settlements;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Settlements.ConfirmSent;

public class ConfirmSentCommandHandlerTests : SettlementCommandTestBase
{
    private readonly ConfirmSentCommandHandler _sut;

    public ConfirmSentCommandHandlerTests()
    {
        _sut = new ConfirmSentCommandHandler(
            Data.Object, Guard.Object, NullLogger<ConfirmSentCommandHandler>.Instance);
    }

    private void ArrangeSettlement(Guid payerFamilyId, SettlementStatus status) =>
        Data.Setup(d => d.GetSettlementForConfirmAsync(SettlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettlementForConfirm(
                SettlementId, ActivityId, GroupId, payerFamilyId, OtherFamilyId, 50m, "EUR", status));

    private static void VerifyNeverPersisted(Mock<ISettlementData> data) =>
        data.Verify(d => d.ConfirmSentAsync(It.IsAny<Guid>(), It.IsAny<ApprovalStep>(),
            It.IsAny<AuditEntry>(), It.IsAny<SettlementNotification>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_SettlementNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetSettlementForConfirmAsync(SettlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SettlementForConfirm?)null);

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Settlement not found.*");
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_CallerNotGroupMember_ThrowsForbidden()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.Proposed);
        ArrangeNotGroupMember();

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_CallerNotPayerFamily_ThrowsForbidden()
    {
        // The payer is OtherFamily, but the caller resolves to CallerFamily.
        ArrangeSettlement(OtherFamilyId, SettlementStatus.Proposed);

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*paying family*");
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_StatusNotProposed_ThrowsValidation()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.PayerSent);

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*expected Proposed*");
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_ValidProposed_PersistsStepAndNotifiesReceiver()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.Proposed);

        ApprovalStep? step = null;
        SettlementNotification? notify = null;
        Data.Setup(d => d.ConfirmSentAsync(SettlementId, It.IsAny<ApprovalStep>(),
                It.IsAny<AuditEntry>(), It.IsAny<SettlementNotification>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ApprovalStep, AuditEntry, SettlementNotification, CancellationToken>((_, s, _, n, _) => { step = s; notify = n; })
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(SettlementId, CallerId, CT);

        step.Should().NotBeNull();
        step!.StepType.Should().Be(StepType.PayerSent);
        step.ApproverId.Should().Be(CallerId);
        step.SettlementId.Should().Be(SettlementId);
        notify!.TargetFamilyId.Should().Be(OtherFamilyId);
    }
}
