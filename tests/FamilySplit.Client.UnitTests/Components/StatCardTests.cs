using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;
using MudBlazor;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for <see cref="StatCard"/>.
/// StatCard has no I18nText dependency — text is passed directly as props.
/// </summary>
public sealed class StatCardTests : BunitTestContext
{
    [Fact]
    public void Renders_Value_Text()
    {
        var cut = Render<StatCard>(p => p
            .Add(x => x.Icon, Icons.Material.Filled.List)
            .Add(x => x.Value, "42")
            .Add(x => x.Label, "Activities"));

        cut.Markup.Should().Contain("42");
    }

    [Fact]
    public void Renders_Label_Text()
    {
        var cut = Render<StatCard>(p => p
            .Add(x => x.Icon, Icons.Material.Filled.List)
            .Add(x => x.Value, "7")
            .Add(x => x.Label, "Expenses"));

        cut.Markup.Should().Contain("Expenses");
    }

    [Fact]
    public void Different_Values_Produce_Different_Markup()
    {
        var cut1 = Render<StatCard>(p => p
            .Add(x => x.Icon, Icons.Material.Filled.List)
            .Add(x => x.Value, "1")
            .Add(x => x.Label, "One"));

        var cut2 = Render<StatCard>(p => p
            .Add(x => x.Icon, Icons.Material.Filled.List)
            .Add(x => x.Value, "99")
            .Add(x => x.Label, "NinetyNine"));

        cut1.Markup.Should().NotBe(cut2.Markup);
    }
}
