using FamilySplit.Application.Settlements;
using FamilySplit.Domain.Enums;

namespace FamilySplit.UnitTests.Core;

public class BusinessGuardTests
{
    // ── ExpenseReshuffleRequired moved to Features/Expenses/Shared with the slice ─
    // ── ActivityCloseGuard moved to Features/Activities/Shared with the slice ─────

    // ── SettlementStateMachine ────────────────────────────────────────────────

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
