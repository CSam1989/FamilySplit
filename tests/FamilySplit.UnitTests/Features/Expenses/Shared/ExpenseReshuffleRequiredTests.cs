using FamilySplit.Features.Expenses.Shared;

namespace FamilySplit.UnitTests.Features.Expenses.Shared;

public class ExpenseReshuffleRequiredTests
{
    [Fact]
    public void Check_AmountUnchanged_DateUnchanged_ReturnsFalse()
    {
        ExpenseReshuffleRequired.Check(10m, 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1))
            .Should().BeFalse();
    }

    [Fact]
    public void Check_AmountChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 20m, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1))
            .Should().BeTrue();
    }

    [Fact]
    public void Check_DateChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 10m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1))
            .Should().BeTrue();
    }

    [Fact]
    public void Check_BothChanged_ReturnsTrue()
    {
        ExpenseReshuffleRequired.Check(10m, 20m, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1))
            .Should().BeTrue();
    }
}
