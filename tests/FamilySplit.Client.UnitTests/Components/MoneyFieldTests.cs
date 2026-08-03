using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for <see cref="MoneyField"/> — the culture-tolerant money input
/// that replaces MudNumericField for amounts (iOS-EU comma keypads).
/// </summary>
public sealed class MoneyFieldTests : BunitTestContext
{
    [Fact]
    public void Renders_Label_And_DecimalInputMode()
    {
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount (EUR)"));

        cut.Markup.Should().Contain("Amount (EUR)");
        cut.Find("input").GetAttribute("inputmode").Should().Be("decimal");
    }

    [Fact]
    public void Renders_DataTestId_On_Input()
    {
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.DataTestId, "input-expense-amount"));

        cut.Find("input[data-testid='input-expense-amount']").Should().NotBeNull();
    }

    [Fact]
    public async Task Typing_CommaDecimal_Binds_CorrectValue()
    {
        decimal? captured = null;
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.ValueChanged, (decimal? v) => captured = v));

        // Awaiting InvokeAsync prevents stale event handler IDs caused by
        // I18nText's async re-render completing after Render<T>() returns.
        await cut.InvokeAsync(() => cut.Find("input").Input("4,50"));

        cut.WaitForAssertion(() => captured.Should().Be(4.50m));
    }

    [Fact]
    public async Task Typing_DotDecimal_Binds_CorrectValue()
    {
        decimal? captured = null;
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.ValueChanged, (decimal? v) => captured = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("12.50"));

        cut.WaitForAssertion(() => captured.Should().Be(12.50m));
    }

    [Fact]
    public async Task Typing_GroupedNumber_Binds_Null()
    {
        decimal? captured = 1m;
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.Value, 5m)
            .Add(x => x.ValueChanged, (decimal? v) => captured = v));

        await cut.InvokeAsync(() => cut.Find("input").Input("1.234.56"));

        cut.WaitForAssertion(() => captured.Should().BeNull());
    }

    [Fact]
    public void InitialValue_Is_Displayed_With_Two_Decimals()
    {
        var cut = Render<MoneyField>(p => p
            .Add(x => x.Label, "Amount")
            .Add(x => x.Value, 25m));

        cut.WaitForAssertion(() =>
            cut.Find("input").GetAttribute("value").Should().Be("25.00"));
    }
}
