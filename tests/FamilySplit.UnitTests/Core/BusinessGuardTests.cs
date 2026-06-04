using FamilySplit.Application.Activities;
using FamilySplit.Application.Expenses;
using FamilySplit.Application.Settlements;
using FamilySplit.Domain.Enums;

namespace FamilySplit.UnitTests.Core;

public class BusinessGuardTests
{
    // ── ExpenseReshuffleRequired ──────────────────────────────────────────────

    [Fact]
    public void ExpenseReshuffle_AmountUnchanged_DateUnchanged_ReturnsFalse()
    {
        ExpenseReshuffleRequired.Check(10m, 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1))
            .Should().BeFalse();
    }

    [Fact]
    public void ExpenseReshuffle_AmountChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 20m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1))
            .Should().BeTrue();
    }

    [Fact]
    public void ExpenseReshuffle_DateChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1))
            .Should().BeTrue();
    }

    [Fact]
    public void ExpenseReshuffle_BothChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 20m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1))
            .Should().BeTrue();
    }

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
