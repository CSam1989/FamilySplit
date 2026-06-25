using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.ConfirmReceived;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.UnitTests.Features.Settlements;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Settlements.ConfirmReceived;

public class ConfirmReceivedCommandHandlerTests : SettlementCommandTestBase
{
    private readonly ConfirmReceivedCommandHandler _sut;

    public ConfirmReceivedCommandHandlerTests()
    {
        _sut = new ConfirmReceivedCommandHandler(
            Data.Object, Guard.Object, NullLogger<ConfirmReceivedCommandHandler>.Instance);
    }

    private void ArrangeSettlement(Guid receiverFamilyId, SettlementStatus status) =>
        Data.Setup(d => d.GetSettlementForConfirmAsync(SettlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettlementForConfirm(
                SettlementId, ActivityId, GroupId, OtherFamilyId, receiverFamilyId, 50m, "EUR", status));

    private static void VerifyNeverPersisted(Mock<ISettlementData> data) =>
        data.Verify(d => d.ConfirmReceivedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<ApprovalStep>(), It.IsAny<AuditEntry>(), It.IsAny<bool>(),
            It.IsAny<SettlementNotification>(), It.IsAny<CancellationToken>()), Times.Never);

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
    public async Task Handle_CallerNotReceiverFamily_ThrowsForbidden()
    {
        // The receiver is OtherFamily, but the caller resolves to CallerFamily.
        ArrangeSettlement(OtherFamilyId, SettlementStatus.PayerSent);

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*receiving family*");
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_StatusNotPayerSent_ThrowsValidation()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.Proposed);

        Func<Task> act = () => _sut.HandleAsync(SettlementId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*expected PayerSent*");
        VerifyNeverPersisted(Data);
    }

    [Fact]
    public async Task Handle_LastOutstandingSettlement_MarksActivitySettled()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.PayerSent);
        Data.Setup(d => d.AreOtherSettlementsCompletedAsync(ActivityId, SettlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.HandleAsync(SettlementId, CallerId, CT);

        Data.Verify(d => d.ConfirmReceivedAsync(SettlementId, ActivityId, It.IsAny<DateTimeOffset>(),
            It.Is<ApprovalStep>(s => s.StepType == StepType.ReceiverConfirmed && s.ApproverId == CallerId),
            It.IsAny<AuditEntry>(), true,
            It.Is<SettlementNotification>(n => n.TargetFamilyId == OtherFamilyId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OtherSettlementsStillPending_DoesNotMarkActivitySettled()
    {
        ArrangeSettlement(CallerFamilyId, SettlementStatus.PayerSent);
        Data.Setup(d => d.AreOtherSettlementsCompletedAsync(ActivityId, SettlementId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.HandleAsync(SettlementId, CallerId, CT);

        Data.Verify(d => d.ConfirmReceivedAsync(SettlementId, ActivityId, It.IsAny<DateTimeOffset>(),
            It.IsAny<ApprovalStep>(), It.IsAny<AuditEntry>(), false,
            It.IsAny<SettlementNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
