using FamilySplit.Application.Core;
using FluentAssertions;

namespace FamilySplit.UnitTests.Core;

public class SettlementOptimiserTests
{
    private static readonly Guid FamilyA = Guid.NewGuid();
    private static readonly Guid FamilyB = Guid.NewGuid();
    private static readonly Guid FamilyC = Guid.NewGuid();
    private static readonly Guid FamilyD = Guid.NewGuid();

    [Fact]
    public void Optimise_EmptyBalances_ReturnsEmptyList()
    {
        var result = SettlementOptimiser.Optimise([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_AllNearZeroBalances_ReturnsEmptyList()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 0.00001m,
            [FamilyB] = -0.00001m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_ExactlyZeroBalances_ReturnsEmptyList()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 0m,
            [FamilyB] = 0m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_OneDebtorOneCreditorEqualAmounts_ReturnsSingleTransfer()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -100m,
            [FamilyB] = 100m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(1);
        result[0].PayerFamilyId.Should().Be(FamilyA);
        result[0].ReceiverFamilyId.Should().Be(FamilyB);
        result[0].Amount.Should().Be(100m);
    }

    [Fact]
    public void Optimise_DebtorOwesLessThanCreditorIsOwed_SplitsCredit()
    {
        // A owes 30, B owes 70; C is owed 100
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -30m,
            [FamilyB] = -70m,
            [FamilyC] = 100m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.Sum(t => t.Amount).Should().Be(100m);
        result.All(t => t.ReceiverFamilyId == FamilyC).Should().BeTrue();
    }

    [Fact]
    public void Optimise_OneDebtorOwesMultipleCreditors_ProducesMultipleTransfers()
    {
        // A is owed 40, B is owed 60; C owes 100
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 40m,
            [FamilyB] = 60m,
            [FamilyC] = -100m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.All(t => t.PayerFamilyId == FamilyC).Should().BeTrue();
        result.Sum(t => t.Amount).Should().Be(100m);
    }

    [Fact]
    public void Optimise_MultipleDebtorsMultipleCreditors_TotalTransfersBalance()
    {
        // A owes 50, B owes 50; C is owed 60, D is owed 40
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -50m,
            [FamilyB] = -50m,
            [FamilyC] = 60m,
            [FamilyD] = 40m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().NotBeEmpty();
        result.Sum(t => t.Amount).Should().Be(100m);
    }

    [Fact]
    public void Optimise_UnbalancedInput_StillProducesCorrectTransfers()
    {
        // Only a creditor, no debtor with significant amount
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 100m,
            [FamilyB] = 0.000001m,  // near zero, ignored
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_AmountRoundedToTwoDecimalPlaces()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -33.333m,
            [FamilyB] = 33.333m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(1);
        result[0].Amount.Should().Be(33.33m);
    }

    [Fact]
    public void Optimise_OnlyDebtors_ReturnsEmptyList()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -50m,
            [FamilyB] = -50m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_OnlyCreditors_ReturnsEmptyList()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 50m,
            [FamilyB] = 50m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimise_MidpointRounding_UsesAwayFromZero()
    {
        // 2.225 rounded to 2 decimal places should be 2.23 (away from zero)
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -2.225m,
            [FamilyB] = 2.225m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(1);
        result[0].Amount.Should().Be(2.23m);
    }

    [Fact]
    public void Optimise_PayerAndReceiverAssignedCorrectly()
    {
        var debtor = Guid.NewGuid();
        var creditor = Guid.NewGuid();
        var balances = new Dictionary<Guid, decimal>
        {
            [debtor] = -200m,
            [creditor] = 200m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(1);
        result[0].PayerFamilyId.Should().Be(debtor);
        result[0].ReceiverFamilyId.Should().Be(creditor);
    }

    [Fact]
    public void Optimise_DebtorLargerThanCreditor_AdvancesCreditorFirst()
    {
        // One debtor owes 100, two creditors owed 60 and 40
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -100m,
            [FamilyB] = 60m,
            [FamilyC] = 40m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.PayerFamilyId.Should().Be(FamilyA));
        result.Sum(t => t.Amount).Should().Be(100m);
    }

    [Fact]
    public void Optimise_CreditorLargerThanDebtor_AdvancesDebtorFirst()
    {
        // Two debtors owe 30 and 70, one creditor owed 100
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -70m,
            [FamilyB] = -30m,
            [FamilyC] = 100m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.ReceiverFamilyId.Should().Be(FamilyC));
        result[0].Amount.Should().Be(70m);
        result[1].Amount.Should().Be(30m);
    }

    [Fact]
    public void Optimise_EqualDebtAndCredit_BothAdvanceTogether()
    {
        // Two debtors and two creditors with matching amounts
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -50m,
            [FamilyB] = -50m,
            [FamilyC] = 50m,
            [FamilyD] = 50m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.Sum(t => t.Amount).Should().Be(100m);
        result.Should().AllSatisfy(t => t.Amount.Should().Be(50m));
    }

    [Fact]
    public void Optimise_FractionalAmounts_ProducesCorrectTransfers()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = -10.50m,
            [FamilyB] = -9.50m,
            [FamilyC] = 20.00m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().HaveCount(2);
        result.Sum(t => t.Amount).Should().Be(20.00m);
    }

    [Fact]
    public void Optimise_SingleEntryNearZero_ReturnsEmpty()
    {
        var balances = new Dictionary<Guid, decimal>
        {
            [FamilyA] = 0.004m,
        };

        var result = SettlementOptimiser.Optimise(balances);

        result.Should().BeEmpty();
    }
}
