using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>bUnit tests for <see cref="SectionHeader"/>.</summary>
public sealed class SectionHeaderTests : BunitTestContext
{
    [Fact]
    public void Renders_Title()
    {
        var cut = Render<SectionHeader>(p => p
            .Add(x => x.Title, "Members"));

        cut.Markup.Should().Contain("Members");
    }

    [Fact]
    public void Actions_Render_When_Provided()
    {
        var cut = Render<SectionHeader>(p => p
            .Add(x => x.Title, "Expenses")
            .Add(x => x.Actions, "<button>Add expense</button>"));

        cut.Find("button").TextContent.Should().Contain("Add expense");
    }

    [Fact]
    public void Actions_Absent_When_Not_Provided()
    {
        var cut = Render<SectionHeader>(p => p
            .Add(x => x.Title, "Expenses"));

        cut.FindAll("button").Should().BeEmpty();
    }
}
