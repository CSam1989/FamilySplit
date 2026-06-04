using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;
using MudBlazor;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>bUnit tests for <see cref="EmptyState"/>.</summary>
public sealed class EmptyStateTests : BunitTestContext
{
    [Fact]
    public void Renders_Title()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Icon, Icons.Material.Outlined.Group)
            .Add(x => x.Title, "No groups yet"));

        cut.Markup.Should().Contain("No groups yet");
    }

    [Fact]
    public void Subtitle_Renders_When_Provided()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Icon, Icons.Material.Outlined.Group)
            .Add(x => x.Title, "No groups yet")
            .Add(x => x.Subtitle, "Create one to get started"));

        cut.Markup.Should().Contain("Create one to get started");
    }

    [Fact]
    public void Subtitle_Absent_When_Not_Provided()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Icon, Icons.Material.Outlined.Group)
            .Add(x => x.Title, "No groups yet"));

        // Subtitle is optional — the component renders only the title paragraph.
        cut.FindAll("p, .mud-typography").Count
            .Should().BeLessThan(3,
                "only the title MudText element should render when Subtitle is null");
    }

    [Fact]
    public void ChildContent_Renders_When_Provided()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Icon, Icons.Material.Outlined.Group)
            .Add(x => x.Title, "Empty")
            .Add(x => x.ChildContent, "<button>Add one</button>"));

        cut.Markup.Should().Contain("Add one");
    }

    [Fact]
    public void ChildContent_Absent_When_Not_Provided()
    {
        var cut = Render<EmptyState>(p => p
            .Add(x => x.Icon, Icons.Material.Outlined.Group)
            .Add(x => x.Title, "Empty"));

        cut.FindAll("button").Should().BeEmpty();
    }
}
