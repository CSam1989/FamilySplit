using FamilySplit.Application.Core;
using FluentAssertions;
using ExpenseData = FamilySplit.Application.Core.BalanceCalculator.ExpenseData;
using ParticipantData = FamilySplit.Application.Core.BalanceCalculator.ParticipantData;

namespace FamilySplit.UnitTests.Core;

public class BalanceCalculatorTests
{
    private static readonly Guid FamilyA = Guid.NewGuid();
    private static readonly Guid FamilyB = Guid.NewGuid();
    private static readonly Guid FamilyC = Guid.NewGuid();

    [Fact]
    public void Compute_EmptyInputs_ReturnsEmptyDictionary()
    {
        var result = BalanceCalculator.Compute([], []);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compute_SingleExpenseNoParticipants_CreditsPayer()
    {
        var expenses = new[] { new ExpenseData(FamilyA, 100m) };

        var result = BalanceCalculator.Compute(expenses, []);

        result.Should().ContainKey(FamilyA).WhoseValue.Should().Be(100m);
    }

    [Fact]
    public void Compute_NoExpensesSingleParticipant_DebitsParticipant()
    {
        var participants = new[] { new ParticipantData(FamilyA, 50m) };

        var result = BalanceCalculator.Compute([], participants);

        result.Should().ContainKey(FamilyA).WhoseValue.Should().Be(-50m);
    }

    [Fact]
    public void Compute_PayerIsAlsoParticipant_NetBalanceReturned()
    {
        // FamilyA paid 100, owes 40 → net +60
        var expenses = new[] { new ExpenseData(FamilyA, 100m) };
        var participants = new[] { new ParticipantData(FamilyA, 40m) };

        var result = BalanceCalculator.Compute(expenses, participants);

        result[FamilyA].Should().Be(60m);
    }

    [Fact]
    public void Compute_TwoFamiliesOnePayerOneParticipant_CorrectBalances()
    {
        // FamilyA pays 100, FamilyB owes 100
        var expenses = new[] { new ExpenseData(FamilyA, 100m) };
        var participants = new[] { new ParticipantData(FamilyB, 100m) };

        var result = BalanceCalculator.Compute(expenses, participants);

        result[FamilyA].Should().Be(100m);
        result[FamilyB].Should().Be(-100m);
    }

    [Fact]
    public void Compute_MultipleExpensesSamePayer_AccumulatesCredit()
    {
        var expenses = new[]
        {
            new ExpenseData(FamilyA, 60m),
            new ExpenseData(FamilyA, 40m),
        };

        var result = BalanceCalculator.Compute(expenses, []);

        result[FamilyA].Should().Be(100m);
    }

    [Fact]
    public void Compute_MultipleParticipantsSameFamily_AccumulatesDebit()
    {
        var participants = new[]
        {
            new ParticipantData(FamilyB, 30m),
            new ParticipantData(FamilyB, 20m),
        };

        var result = BalanceCalculator.Compute([], participants);

        result[FamilyB].Should().Be(-50m);
    }

    [Fact]
    public void Compute_ThreeFamilies_EachHasCorrectBalance()
    {
        // FamilyA pays 120; FamilyA owes 40, FamilyB owes 40, FamilyC owes 40
        var expenses = new[] { new ExpenseData(FamilyA, 120m) };
        var participants = new[]
        {
            new ParticipantData(FamilyA, 40m),
            new ParticipantData(FamilyB, 40m),
            new ParticipantData(FamilyC, 40m),
        };

        var result = BalanceCalculator.Compute(expenses, participants);

        result[FamilyA].Should().Be(80m);   // 120 - 40
        result[FamilyB].Should().Be(-40m);
        result[FamilyC].Should().Be(-40m);
    }

    [Fact]
    public void Compute_MultipleExpensesMultiplePayers_CorrectPerFamilyBalance()
    {
        var expenses = new[]
        {
            new ExpenseData(FamilyA, 100m),
            new ExpenseData(FamilyB, 50m),
        };
        var participants = new[]
        {
            new ParticipantData(FamilyA, 50m),
            new ParticipantData(FamilyB, 50m),
            new ParticipantData(FamilyC, 50m),
        };

        var result = BalanceCalculator.Compute(expenses, participants);

        result[FamilyA].Should().Be(50m);   // 100 - 50
        result[FamilyB].Should().Be(0m);    // 50 - 50
        result[FamilyC].Should().Be(-50m);
    }

    [Fact]
    public void Compute_ZeroAmountExpenseAndParticipant_ReturnsZeroBalance()
    {
        var expenses = new[] { new ExpenseData(FamilyA, 0m) };
        var participants = new[] { new ParticipantData(FamilyA, 0m) };

        var result = BalanceCalculator.Compute(expenses, participants);

        result[FamilyA].Should().Be(0m);
    }

    [Fact]
    public void Compute_FamilyOnlyInParticipants_AppearsInResult()
    {
        var participants = new[] { new ParticipantData(FamilyC, 75m) };

        var result = BalanceCalculator.Compute([], participants);

        result.Should().ContainKey(FamilyC);
        result[FamilyC].Should().Be(-75m);
    }

    [Fact]
    public void Compute_FamilyOnlyInExpenses_AppearsInResult()
    {
        var expenses = new[] { new ExpenseData(FamilyC, 75m) };

        var result = BalanceCalculator.Compute(expenses, []);

        result.Should().ContainKey(FamilyC);
        result[FamilyC].Should().Be(75m);
    }
}
