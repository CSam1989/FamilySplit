using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Components;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for <see cref="MemberActionCell"/>.
///
/// Key behaviours:
///  • Edit button always renders.
///  • Remove button rendered when ShowRemove=true AND MemberId != CallerId.
///  • Remove button hidden when MemberId == CallerId (self-guard).
///  • Remove button hidden when ShowRemove=false (non-admin caller).
///  • Remove button carries the correct data-testid for E2E targeting.
///  • Clicking each button invokes the corresponding callback.
/// </summary>
public sealed class MemberActionCellTests : BunitTestContext
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid CallerId = Guid.NewGuid();  // different from MemberId

    // ── Edit button ───────────────────────────────────────────────────────────

    [Fact]
    public void Edit_Button_Always_Renders()
    {
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.CallerId, CallerId)
            .Add(x => x.ShowRemove, false));

        // The Edit icon button must always be present — even for non-admins
        // (admins pass ShowRemove; non-admins don't see the cell at all, but
        // when the cell is rendered the Edit button is unconditional).
        cut.FindAll("button").Should().NotBeEmpty("Edit button must always render");
    }

    [Fact]
    public async Task Edit_Click_Invokes_Callback()
    {
        var invoked = false;
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.CallerId, CallerId)
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, () => { invoked = true; })));

        // Awaiting InvokeAsync prevents stale event handler IDs caused by
        // I18nText's async re-render completing after Render<T>() returns.
        await cut.InvokeAsync(() => cut.FindAll("button").First().Click());

        invoked.Should().BeTrue("clicking Edit must invoke OnEdit");
    }

    // ── Remove button ─────────────────────────────────────────────────────────

    [Fact]
    public void Remove_Button_Shown_When_ShowRemove_And_Not_Self()
    {
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.CallerId, CallerId)   // different — not self
            .Add(x => x.ShowRemove, true)
            .Add(x => x.OnRemove, EventCallback.Empty));

        cut.Find($"[data-testid='btn-remove-member-{MemberId}']")
           .Should().NotBeNull("Remove renders when ShowRemove=true and member != caller");
    }

    [Fact]
    public void Remove_Button_Hidden_When_MemberId_Equals_CallerId()
    {
        // Self-guard: even with ShowRemove=true, the user cannot remove themselves.
        var selfId = Guid.NewGuid();
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, selfId)
            .Add(x => x.CallerId, selfId)   // same
            .Add(x => x.ShowRemove, true)
            .Add(x => x.OnRemove, EventCallback.Empty));

        cut.FindAll($"[data-testid='btn-remove-member-{selfId}']")
           .Should().BeEmpty("Remove must be hidden when the member IS the caller");
    }

    [Fact]
    public void Remove_Button_Hidden_When_ShowRemove_False()
    {
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.CallerId, CallerId)
            .Add(x => x.ShowRemove, false));    // non-admin

        cut.FindAll($"[data-testid='btn-remove-member-{MemberId}']")
           .Should().BeEmpty("Remove must be hidden for non-admin callers");
    }

    [Fact]
    public void Remove_Button_TestId_Contains_MemberId()
    {
        var specificId = Guid.NewGuid();
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, specificId)
            .Add(x => x.CallerId, Guid.NewGuid())   // different caller
            .Add(x => x.ShowRemove, true)
            .Add(x => x.OnRemove, EventCallback.Empty));

        cut.Find($"[data-testid='btn-remove-member-{specificId}']")
           .Should().NotBeNull("data-testid must embed the specific MemberId");
    }

    [Fact]
    public async Task Remove_Click_Invokes_Callback()
    {
        var invoked = false;
        var cut = Render<MemberActionCell>(p => p
            .Add(x => x.MemberId, MemberId)
            .Add(x => x.CallerId, CallerId)
            .Add(x => x.ShowRemove, true)
            .Add(x => x.OnRemove, EventCallback.Factory.Create(this, () => { invoked = true; })));

        await cut.InvokeAsync(() => cut.Find($"[data-testid='btn-remove-member-{MemberId}']").Click());

        invoked.Should().BeTrue("clicking Remove must invoke OnRemove");
    }
}
