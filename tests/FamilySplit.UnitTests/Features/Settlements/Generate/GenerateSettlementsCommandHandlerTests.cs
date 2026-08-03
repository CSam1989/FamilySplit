using FamilySplit.Common.Auditing;
using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Generate;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.UnitTests.Features.Settlements;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Settlements.Generate;

public class GenerateSettlementsCommandHandlerTests : SettlementCommandTestBase
{
    private readonly GenerateSettlementsCommandHandler _sut;

    public GenerateSettlementsCommandHandlerTests()
    {
        _sut = new GenerateSettlementsCommandHandler(
            Data.Object, Guard.Object, NullLogger<GenerateSettlementsCommandHandler>.Instance);
    }

    private void ArrangeActivity(ActivityStatus status, Guid? parentId = null) =>
        Data.Setup(d => d.GetActivityAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityForSettlement(GroupId, status, parentId));

    private void ArrangeBalances(SettlementBalanceInputs inputs, string currency = "EUR")
    {
        Data.Setup(d => d.GetActivityCurrencyAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(currency);
        Data.Setup(d => d.GetBalanceInputsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(inputs);
    }

    [Fact]
    public async Task Handle_ActivityNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetActivityAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActivityForSettlement?)null);

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Activity not found.*");
        Data.Verify(d => d.GenerateSettlementsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Settlement>>(),
            It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotGroupMember_ThrowsForbidden()
    {
        ArrangeActivity(ActivityStatus.Closed);
        ArrangeNotGroupMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.GenerateSettlementsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Settlement>>(),
            It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AbsorbedByParent_ThrowsValidation()
    {
        ArrangeActivity(ActivityStatus.AbsorbedByParent);

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*absorbed*");
    }

    [Fact]
    public async Task Handle_SubActivity_ThrowsValidation()
    {
        ArrangeActivity(ActivityStatus.Closed, parentId: Guid.NewGuid());

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Sub-activities*");
    }

    [Fact]
    public async Task Handle_ExistingSettlements_NoOp()
    {
        ArrangeActivity(ActivityStatus.Closed);
        Data.Setup(d => d.CountSettlementsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _sut.HandleAsync(ActivityId, CallerId, CT);

        Data.Verify(d => d.GenerateSettlementsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Settlement>>(),
            It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
        Data.Verify(d => d.MarkActivitySettledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SettledWithNoSettlements_NoOp()
    {
        ArrangeActivity(ActivityStatus.Settled);
        Data.Setup(d => d.CountSettlementsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await _sut.HandleAsync(ActivityId, CallerId, CT);

        Data.Verify(d => d.GenerateSettlementsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Settlement>>(),
            It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
        Data.Verify(d => d.MarkActivitySettledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OpenActivity_ThrowsValidation()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.CountSettlementsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*must be closed*");
    }

    [Fact]
    public async Task Handle_ZeroBalances_MarksActivitySettled()
    {
        ArrangeActivity(ActivityStatus.Closed);
        Data.Setup(d => d.CountSettlementsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        ArrangeBalances(new SettlementBalanceInputs([], []));

        await _sut.HandleAsync(ActivityId, CallerId, CT);

        Data.Verify(d => d.MarkActivitySettledAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
        Data.Verify(d => d.GenerateSettlementsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Settlement>>(),
            It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithImbalance_PersistsProposedSettlements()
    {
        ArrangeActivity(ActivityStatus.Closed);
        Data.Setup(d => d.CountSettlementsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        // CallerFamily fronted €100; OtherFamily owes the full €100 → one transfer OtherFamily → CallerFamily.
        var inputs = new SettlementBalanceInputs(
            [new BalanceCalculator.ExpenseData(CallerFamilyId, 100m)],
            [new BalanceCalculator.ParticipantData(OtherFamilyId, 100m)]);
        ArrangeBalances(inputs);

        IReadOnlyList<Settlement>? persisted = null;
        Data.Setup(d => d.GenerateSettlementsAsync(ActivityId, It.IsAny<IReadOnlyList<Settlement>>(),
                It.IsAny<IReadOnlyList<AuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<Settlement>, IReadOnlyList<AuditEntry>, CancellationToken>((_, s, _, _) => persisted = s)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(ActivityId, CallerId, CT);

        Data.Verify(d => d.MarkActivitySettledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        persisted.Should().ContainSingle();
        var settlement = persisted![0];
        settlement.PayerFamilyId.Should().Be(OtherFamilyId);
        settlement.ReceiverFamilyId.Should().Be(CallerFamilyId);
        settlement.Amount.Should().Be(100m);
        settlement.Currency.Should().Be("EUR");
        settlement.Status.Should().Be(SettlementStatus.Proposed);
    }
}
