using FamilySplit.Domain.Enums;
using FamilySplit.Features.Settlements.Shared;
using FluentAssertions;

namespace FamilySplit.UnitTests.Features.Settlements.Shared;

public class SettlementStateMachineTests
{
    [Theory]
    [InlineData(SettlementStatus.Proposed, true)]
    [InlineData(SettlementStatus.PayerSent, false)]
    [InlineData(SettlementStatus.Completed, false)]
    [InlineData(SettlementStatus.Cancelled, false)]
    public void CanConfirmSent_OnlyTrueForProposed(SettlementStatus status, bool expected)
    {
        SettlementStateMachine.CanConfirmSent(status).Should().Be(expected);
    }

    [Theory]
    [InlineData(SettlementStatus.PayerSent, true)]
    [InlineData(SettlementStatus.Proposed, false)]
    [InlineData(SettlementStatus.Completed, false)]
    [InlineData(SettlementStatus.Cancelled, false)]
    public void CanConfirmReceived_OnlyTrueForPayerSent(SettlementStatus status, bool expected)
    {
        SettlementStateMachine.CanConfirmReceived(status).Should().Be(expected);
    }
}
