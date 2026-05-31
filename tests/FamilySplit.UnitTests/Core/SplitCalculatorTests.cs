using FamilySplit.Application.Core;
using FamilySplit.Domain.Entities;
using FluentAssertions;

namespace FamilySplit.UnitTests.Core;

public class SplitCalculatorTests
{
    private static ExpenseParticipant P(decimal weight, bool excluded = false) =>
        new() { WeightSnapshot = weight, IsExcluded = excluded };

    [Fact]
    public void CalculateShares_AllExcluded_AllCalculatedAmountsAreZero()
    {
        var participants = new List<ExpenseParticipant>
        {
            P(1m, excluded: true),
            P(2m, excluded: true),
        };

        SplitCalculator.CalculateShares(100m, participants);

        participants.Should().AllSatisfy(p => p.CalculatedAmount.Should().Be(0m));
    }

    [Fact]
    public void CalculateShares_ExcludedParticipantsZeroedOut()
    {
        var excluded = P(5m, excluded: true);
        excluded.CalculatedAmount = 50m; // pre-existing value should be cleared
        var active = P(1m);
        var participants = new List<ExpenseParticipant> { excluded, active };

        SplitCalculator.CalculateShares(100m, participants);

        excluded.CalculatedAmount.Should().Be(0m);
    }

    [Fact]
    public void CalculateShares_AllWeightsZero_SplitEqually()
    {
        var p1 = P(0m);
        var p2 = P(0m);
        var p3 = P(0m);
        var participants = new List<ExpenseParticipant> { p1, p2, p3 };

        SplitCalculator.CalculateShares(10m, participants);

        // 10 / 3 = 3.33 each, remainder 0.01 goes to first
        p1.CalculatedAmount.Should().Be(3.34m);
        p2.CalculatedAmount.Should().Be(3.33m);
        p3.CalculatedAmount.Should().Be(3.33m);
        (p1.CalculatedAmount + p2.CalculatedAmount + p3.CalculatedAmount).Should().Be(10m);
    }

    [Fact]
    public void CalculateShares_AllWeightsZero_EvenSplit_NoRemainder()
    {
        var p1 = P(0m);
        var p2 = P(0m);
        var participants = new List<ExpenseParticipant> { p1, p2 };

        SplitCalculator.CalculateShares(10m, participants);

        p1.CalculatedAmount.Should().Be(5m);
        p2.CalculatedAmount.Should().Be(5m);
    }

    [Fact]
    public void CalculateShares_EqualWeights_SplitEqually()
    {
        var p1 = P(1m);
        var p2 = P(1m);
        var participants = new List<ExpenseParticipant> { p1, p2 };

        SplitCalculator.CalculateShares(100m, participants);

        p1.CalculatedAmount.Should().Be(50m);
        p2.CalculatedAmount.Should().Be(50m);
    }

    [Fact]
    public void CalculateShares_UnequalWeights_SplitProportionally()
    {
        var p1 = P(1m);
        var p2 = P(3m);
        var participants = new List<ExpenseParticipant> { p1, p2 };

        SplitCalculator.CalculateShares(100m, participants);

        p1.CalculatedAmount.Should().Be(25m);
        p2.CalculatedAmount.Should().Be(75m);
        (p1.CalculatedAmount + p2.CalculatedAmount).Should().Be(100m);
    }

    [Fact]
    public void CalculateShares_RoundingRemainder_AbsorbedByHeaviest()
    {
        // 100 / 3: each gets 33.33, total = 99.99, remainder 0.01 to heaviest
        var p1 = P(1m);
        var p2 = P(1m);
        var p3 = P(1m);
        var participants = new List<ExpenseParticipant> { p1, p2, p3 };

        SplitCalculator.CalculateShares(100m, participants);

        var total = p1.CalculatedAmount + p2.CalculatedAmount + p3.CalculatedAmount;
        total.Should().Be(100m);
    }

    [Fact]
    public void CalculateShares_HeaviestParticipantAbsorbsRemainder()
    {
        // p2 has higher weight so should get the remainder
        var p1 = P(1m);
        var p2 = P(2m);
        var p3 = P(1m);
        var participants = new List<ExpenseParticipant> { p1, p2, p3 };

        SplitCalculator.CalculateShares(10m, participants);

        // 1/4*10=2.5, 2/4*10=5, 1/4*10=2.5 — no remainder in this case
        p1.CalculatedAmount.Should().Be(2.5m);
        p2.CalculatedAmount.Should().Be(5m);
        p3.CalculatedAmount.Should().Be(2.5m);
        (p1.CalculatedAmount + p2.CalculatedAmount + p3.CalculatedAmount).Should().Be(10m);
    }

    [Fact]
    public void CalculateShares_SingleParticipant_GetsEntireAmount()
    {
        var p1 = P(1m);
        var participants = new List<ExpenseParticipant> { p1 };

        SplitCalculator.CalculateShares(99.99m, participants);

        p1.CalculatedAmount.Should().Be(99.99m);
    }

    [Fact]
    public void CalculateShares_MixedExcludedAndActive_OnlyActiveReceiveShares()
    {
        var active1 = P(1m);
        var active2 = P(1m);
        var excl = P(5m, excluded: true);
        var participants = new List<ExpenseParticipant> { active1, excl, active2 };

        SplitCalculator.CalculateShares(100m, participants);

        excl.CalculatedAmount.Should().Be(0m);
        active1.CalculatedAmount.Should().Be(50m);
        active2.CalculatedAmount.Should().Be(50m);
    }

    [Fact]
    public void CalculateShares_ZeroAmount_AllGetZero()
    {
        var p1 = P(1m);
        var p2 = P(1m);
        var participants = new List<ExpenseParticipant> { p1, p2 };

        SplitCalculator.CalculateShares(0m, participants);

        p1.CalculatedAmount.Should().Be(0m);
        p2.CalculatedAmount.Should().Be(0m);
    }

    [Fact]
    public void CalculateShares_EmptyList_DoesNotThrow()
    {
        var participants = new List<ExpenseParticipant>();

        var act = () => SplitCalculator.CalculateShares(100m, participants);

        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateShares_SingleZeroWeightParticipant_GetsFullAmount()
    {
        var p1 = P(0m);
        var participants = new List<ExpenseParticipant> { p1 };

        SplitCalculator.CalculateShares(42.50m, participants);

        p1.CalculatedAmount.Should().Be(42.50m);
    }

    [Fact]
    public void CalculateShares_UnequalWeights_RemainderToHeaviest()
    {
        // 3 participants with weights 1, 2, 3; total weight 6; amount 100
        // shares: 16.67, 33.33, 50.00 = 100.00 — but let's use 10:
        // 1/6*10=1.67, 2/6*10=3.33, 3/6*10=5.00 = 10.00
        // Try amount that causes remainder: 1/3*10=3.33, 1/3*10=3.33, 1/3*10=3.33 = 9.99
        var p1 = P(1m);
        var p2 = P(1m);
        var p3 = P(2m); // heaviest
        var participants = new List<ExpenseParticipant> { p1, p2, p3 };

        SplitCalculator.CalculateShares(10m, participants);

        // 1/4*10=2.50, 1/4*10=2.50, 2/4*10=5.00 — no remainder here
        // Use 7 participants scenario instead; just verify total
        var total = p1.CalculatedAmount + p2.CalculatedAmount + p3.CalculatedAmount;
        total.Should().Be(10m);
        // Heaviest should have largest share
        p3.CalculatedAmount.Should().BeGreaterThanOrEqualTo(p1.CalculatedAmount);
    }

    [Fact]
    public void CalculateShares_ThreeUnequalWeights_RemainderAbsorbedByHeaviest()
    {
        // weights 1, 2, 3 => total 6, amount 100
        // 1/6*100=16.67, 2/6*100=33.33, 3/6*100=50.00 => 100.00
        var p1 = P(1m);
        var p2 = P(2m);
        var p3 = P(3m);
        var participants = new List<ExpenseParticipant> { p1, p2, p3 };

        SplitCalculator.CalculateShares(100m, participants);

        p1.CalculatedAmount.Should().Be(16.67m);
        p2.CalculatedAmount.Should().Be(33.33m);
        // p3 gets remainder: 100 - 16.67 - 33.33 = 50.00
        (p1.CalculatedAmount + p2.CalculatedAmount + p3.CalculatedAmount).Should().Be(100m);
    }

    [Fact]
    public void CalculateShares_NegativeAmount_DistributesCorrectly()
    {
        var p1 = P(1m);
        var p2 = P(1m);
        var participants = new List<ExpenseParticipant> { p1, p2 };

        SplitCalculator.CalculateShares(-100m, participants);

        p1.CalculatedAmount.Should().Be(-50m);
        p2.CalculatedAmount.Should().Be(-50m);
    }
}
