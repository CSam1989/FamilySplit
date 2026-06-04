using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for <see cref="MemberRoleChip"/> and <see cref="MemberStatusChip"/>.
///
/// MemberStatusChip has no I18nText dependency (hardcoded strings).
/// MemberRoleChip injects I18nText — text is empty in tests but Color (CSS class) is verified.
/// </summary>
public sealed class MemberChipTests : BunitTestContext
{
    // ── MemberRoleChip ────────────────────────────────────────────────────────

    [Fact]
    public void MemberRoleChip_Admin_Renders_Warning_Color()
    {
        var cut = Render<MemberRoleChip>(p => p
            .Add(x => x.IsAdmin, true));

        // MudBlazor renders Color.Warning as "mud-chip-color-warning" on the chip element.
        cut.Markup.Should().Contain("mud-chip-color-warning",
            "admin chips use Color.Warning");
    }

    [Fact]
    public void MemberRoleChip_NonAdmin_Renders_Default_Color()
    {
        var cut = Render<MemberRoleChip>(p => p
            .Add(x => x.IsAdmin, false));

        cut.Markup.Should().NotContain("mud-chip-color-warning",
            "non-admin chips must not use warning colour");
    }

    [Fact]
    public void MemberRoleChip_Admin_And_NonAdmin_Produce_Different_Markup()
    {
        var admin = Render<MemberRoleChip>(p => p.Add(x => x.IsAdmin, true));
        var member = Render<MemberRoleChip>(p => p.Add(x => x.IsAdmin, false));

        admin.Markup.Should().NotBe(member.Markup);
    }

    // ── MemberStatusChip ──────────────────────────────────────────────────────

    [Fact]
    public void MemberStatusChip_Linked_Renders_Linked_Text_And_Success_Color()
    {
        var cut = Render<MemberStatusChip>(p => p
            .Add(x => x.IsLinked, true)
            .Add(x => x.HasEmail, true));

        cut.Markup.Should().Contain("Linked");
        cut.Markup.Should().Contain("mud-chip-color-success");
    }

    [Fact]
    public void MemberStatusChip_NotLinked_WithEmail_Renders_Pending_Text()
    {
        var cut = Render<MemberStatusChip>(p => p
            .Add(x => x.IsLinked, false)
            .Add(x => x.HasEmail, true));

        cut.Markup.Should().Contain("Pending");
        cut.Markup.Should().NotContain("mud-chip-color-success");
    }

    [Fact]
    public void MemberStatusChip_NotLinked_NoEmail_Renders_Nothing()
    {
        // When there is no email and no link, no chip is shown.
        var cut = Render<MemberStatusChip>(p => p
            .Add(x => x.IsLinked, false)
            .Add(x => x.HasEmail, false));

        cut.FindAll(".mud-chip").Should().BeEmpty(
            "no chip is rendered for members with no email address");
    }
}
