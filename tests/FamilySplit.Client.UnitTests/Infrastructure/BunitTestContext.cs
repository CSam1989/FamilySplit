using Bunit;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace FamilySplit.Client.UnitTests.Infrastructure;

/// <summary>
/// Base bUnit context with MudBlazor, I18nText, and no-op Fluxor infrastructure
/// pre-registered.
///
/// I18nText: <c>GetTextTableAsync</c> is non-virtual — Moq can't intercept it.
/// We register the real service via <c>AddI18nText()</c> with
/// <c>JSRuntimeMode.Loose</c>; the library falls back to <c>new T()</c> (empty
/// strings) when the fetch returns nothing. Tests assert behaviour, not text.
///
/// MudPopoverProvider: MudBlazor 9 throws at runtime if any MudPopoverBase
/// component initialises without a registered <c>IPopoverService</c> provider.
/// <c>MudPopoverProvider</c> itself cannot be added to the bUnit render tree
/// (it has no <c>ChildContent</c> parameter), and rendering it in the constructor
/// locks the DI container before per-test services can be added.
/// Solution: override <c>IPopoverService</c> with a Loose mock after
/// <c>AddMudServices()</c>; popover components receive <c>null</c> popovers and
/// proceed silently — acceptable for tests that don't test popover behaviour.
///
/// Fluxor: <c>FluxorComponent</c> requires <c>IActionSubscriber</c> and
/// <c>IStore</c>. The no-op mocks prevent null-ref crashes. Individual tests
/// add the <c>IState&lt;T&gt;</c> and <c>IDispatcher</c> mocks they need
/// (safe because no render has occurred in this constructor).
/// </summary>
public class BunitTestContext : Bunit.BunitContext
{
    public BunitTestContext()
    {
        // ── MudBlazor ─────────────────────────────────────────────────────────
        Services.AddMudServices();

        // Override the real IPopoverService so MudPopoverBase-derived components
        // don't throw "Missing <MudPopoverProvider />". The Loose mock returns
        // null from CreatePopoverAsync; MudBlazor null-checks _popover everywhere.
        Services.AddSingleton(new Mock<IPopoverService>(MockBehavior.Loose).Object);

        // ── I18nText ──────────────────────────────────────────────────────────
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddI18nText();

        // ── Fluxor no-ops ─────────────────────────────────────────────────────
        Services.AddSingleton(new Mock<IActionSubscriber>().Object);
        Services.AddSingleton(new Mock<IStore>().Object);

        // No Render<T>() call here — keeping the DI container unlocked so that
        // derived test classes can still add per-test services before their
        // first Render<T>() call.
    }
}
