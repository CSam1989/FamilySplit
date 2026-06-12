using FamilySplit.Application.Activities;
using FamilySplit.Application.Settlements;
using FamilySplit.Domain.Enums;

namespace FamilySplit.UnitTests.Core;

public class BusinessGuardTests
{
    // ── ExpenseReshuffleRequired moved to Features/Expenses/Shared with the slice ─

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

    // ── ActivityCloseGuard ───────────────────────────────────────────────────

    [Theory]
    [InlineData(ActivityStatus.Open, true)]
    [InlineData(ActivityStatus.Closed, false)]
    [InlineData(ActivityStatus.Settled, false)]
    [InlineData(ActivityStatus.AbsorbedByParent, false)]
    public void CanClose_OnlyTrueForOpen(ActivityStatus status, bool expected)
    {
        ActivityCloseGuard.CanClose(status).Should().Be(expected);
    }

    [Fact]
    public void IsTopLevel_NullParent_ReturnsTrue()
    {
        ActivityCloseGuard.IsTopLevel(null).Should().BeTrue();
    }

    [Fact]
    public void IsTopLevel_NonNullParent_ReturnsFalse()
    {
        ActivityCloseGuard.IsTopLevel(Guid.NewGuid()).Should().BeFalse();
    }
}
