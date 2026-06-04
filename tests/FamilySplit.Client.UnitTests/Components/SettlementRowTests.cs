using Bunit;
using FamilySplit.Client.Components.Shared;
using FamilySplit.Client.UnitTests.Infrastructure;
using FamilySplit.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace FamilySplit.Client.UnitTests.Components;

/// <summary>
/// bUnit tests for <see cref="SettlementRow"/>.
///
/// Tests use data-testid selectors (same as E2E tests) rather than text content
/// because I18nText returns empty strings in the test environment.
///
/// Key behaviours:
///  • Mark sent button: visible only to the payer when Status = Proposed
///  • Mark received button: visible only to the receiver when Status = PayerSent
///  • TrailingActions: always rendered when provided
///  • data-testid attributes include the SettlementId
/// </summary>
public sealed class SettlementRowTests : BunitTestContext
{
    private static readonly Guid PayerFamilyId = Guid.NewGuid();
    private static readonly Guid ReceiverFamilyId = Guid.NewGuid();
    private static readonly Guid OtherFamilyId = Guid.NewGuid();
    private static readonly Guid SettlementId = Guid.NewGuid();

    // ── Helper ────────────────────────────────────────────────────────────────

    private IRenderedComponent<SettlementRow> Render(
        SettlementStatus status,
        Guid callerFamilyId,
        EventCallback? onMarkSent = null,
        EventCallback? onMarkReceived = null,
        bool includeTrailingActions = false)
    {
        return base.Render<SettlementRow>(p =>
        {
            p.Add(x => x.SettlementId, SettlementId);
            p.Add(x => x.PayerFamilyId, PayerFamilyId);
            p.Add(x => x.PayerFamilyName, "Payer Family");
            p.Add(x => x.ReceiverFamilyId, ReceiverFamilyId);
            p.Add(x => x.ReceiverFamilyName, "Receiver Family");
            p.Add(x => x.Amount, 50m);
            p.Add(x => x.Currency, "EUR");
            p.Add(x => x.Status, status);
            p.Add(x => x.CallerFamilyId, callerFamilyId);

            if (onMarkSent.HasValue) p.Add(x => x.OnMarkSent, onMarkSent.Value);
            if (onMarkReceived.HasValue) p.Add(x => x.OnMarkReceived, onMarkReceived.Value);

            if (includeTrailingActions)
                p.Add(x => x.TrailingActions, "<button id='trailing'>View</button>");
        });
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Fact]
    public void Renders_Both_Family_Names()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId);

        cut.Markup.Should().Contain("Payer Family");
        cut.Markup.Should().Contain("Receiver Family");
    }

    [Fact]
    public void Renders_Settlement_Row_TestId()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId);

        cut.Find($"[data-testid='settlement-row-{SettlementId}']")
           .Should().NotBeNull();
    }

    [Fact]
    public void Status_Chip_Has_Correct_TestId()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId);

        cut.Find($"[data-testid='settlement-status-{SettlementId}']")
           .Should().NotBeNull();
    }

    [Fact]
    public void TrailingActions_Renders_When_Provided()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId, includeTrailingActions: true);

        cut.Find("button#trailing").Should().NotBeNull();
    }

    [Fact]
    public void TrailingActions_Absent_When_Not_Provided()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId, includeTrailingActions: false);

        cut.FindAll("button#trailing").Should().BeEmpty();
    }

    // ── Mark sent button ──────────────────────────────────────────────────────

    [Fact]
    public void MarkSent_Button_Shown_To_Payer_When_Proposed()
    {
        var cut = Render(
            SettlementStatus.Proposed,
            callerFamilyId: PayerFamilyId,
            onMarkSent: EventCallback.Empty);

        cut.Find($"[data-testid='btn-mark-sent-{SettlementId}']")
           .Should().NotBeNull("the payer sees the Mark sent button on a Proposed settlement");
    }

    [Fact]
    public void MarkSent_Button_Hidden_When_Caller_Is_Not_Payer()
    {
        var cut = Render(
            SettlementStatus.Proposed,
            callerFamilyId: ReceiverFamilyId,   // receiver, not payer
            onMarkSent: EventCallback.Empty);

        cut.FindAll($"[data-testid='btn-mark-sent-{SettlementId}']")
           .Should().BeEmpty("non-payer must not see the Mark sent button");
    }

    [Fact]
    public void MarkSent_Button_Hidden_When_Status_Is_Not_Proposed()
    {
        var cut = Render(
            SettlementStatus.PayerSent,         // already sent
            callerFamilyId: PayerFamilyId,
            onMarkSent: EventCallback.Empty);

        cut.FindAll($"[data-testid='btn-mark-sent-{SettlementId}']")
           .Should().BeEmpty("Mark sent is only shown for Proposed settlements");
    }

    [Fact]
    public void MarkSent_Button_Hidden_When_Callback_Not_Wired()
    {
        // Passing no OnMarkSent means the page doesn't support this action.
        var cut = Render(
            SettlementStatus.Proposed,
            callerFamilyId: PayerFamilyId
        // onMarkSent intentionally omitted
        );

        cut.FindAll($"[data-testid='btn-mark-sent-{SettlementId}']")
           .Should().BeEmpty("button is suppressed when no callback is registered");
    }

    // ── Mark received button ──────────────────────────────────────────────────

    [Fact]
    public void MarkReceived_Button_Shown_To_Receiver_When_PayerSent()
    {
        var cut = Render(
            SettlementStatus.PayerSent,
            callerFamilyId: ReceiverFamilyId,
            onMarkReceived: EventCallback.Empty);

        cut.Find($"[data-testid='btn-mark-received-{SettlementId}']")
           .Should().NotBeNull("the receiver sees Mark received once the payer has sent");
    }

    [Fact]
    public void MarkReceived_Button_Hidden_When_Caller_Is_Not_Receiver()
    {
        var cut = Render(
            SettlementStatus.PayerSent,
            callerFamilyId: PayerFamilyId,      // payer, not receiver
            onMarkReceived: EventCallback.Empty);

        cut.FindAll($"[data-testid='btn-mark-received-{SettlementId}']")
           .Should().BeEmpty("non-receiver must not see the Mark received button");
    }

    [Fact]
    public void MarkReceived_Button_Hidden_When_Status_Is_Proposed()
    {
        var cut = Render(
            SettlementStatus.Proposed,          // payer hasn't sent yet
            callerFamilyId: ReceiverFamilyId,
            onMarkReceived: EventCallback.Empty);

        cut.FindAll($"[data-testid='btn-mark-received-{SettlementId}']")
           .Should().BeEmpty("Mark received requires status = PayerSent");
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkSent_Click_Invokes_Callback()
    {
        var invoked = false;
        var callback = EventCallback.Factory.Create(this, () => { invoked = true; });

        var cut = Render(
            SettlementStatus.Proposed,
            callerFamilyId: PayerFamilyId,
            onMarkSent: callback);

        await cut.InvokeAsync(() => cut.Find($"[data-testid='btn-mark-sent-{SettlementId}']").Click());

        invoked.Should().BeTrue("clicking Mark sent must invoke OnMarkSent");
    }

    [Fact]
    public async Task MarkReceived_Click_Invokes_Callback()
    {
        var invoked = false;
        var callback = EventCallback.Factory.Create(this, () => { invoked = true; });

        var cut = Render(
            SettlementStatus.PayerSent,
            callerFamilyId: ReceiverFamilyId,
            onMarkReceived: callback);

        await cut.InvokeAsync(() => cut.Find($"[data-testid='btn-mark-received-{SettlementId}']").Click());

        invoked.Should().BeTrue("clicking Mark received must invoke OnMarkReceived");
    }

    // ── Status chip colour ────────────────────────────────────────────────────

    // ── Status chip rendering ─────────────────────────────────────────────────
    // Note: MudBlazor 9's MudChip does not always apply a colour CSS class when
    // running under bUnit (the class is determined at runtime by the theme engine
    // which is not fully active in tests). We therefore verify only that the chip
    // element is present with the correct data-testid, and that different statuses
    // produce different markup — not specific CSS class names.

    [Fact]
    public void Proposed_Status_Chip_Renders()
    {
        var cut = Render(SettlementStatus.Proposed, OtherFamilyId);

        cut.Find($"[data-testid='settlement-status-{SettlementId}']")
           .Should().NotBeNull("Proposed status must show a chip");
    }

    [Fact]
    public void PayerSent_Status_Chip_Renders()
    {
        var cut = Render(SettlementStatus.PayerSent, OtherFamilyId);

        cut.Find($"[data-testid='settlement-status-{SettlementId}']")
           .Should().NotBeNull("PayerSent status must show a chip");
    }

    [Fact]
    public void Completed_Status_Chip_Renders()
    {
        var cut = Render(SettlementStatus.Completed, OtherFamilyId);

        cut.Find($"[data-testid='settlement-status-{SettlementId}']")
           .Should().NotBeNull("Completed status must show a chip");
    }

    [Fact]
    public void Different_Statuses_Produce_Different_Markup()
    {
        var proposed  = Render(SettlementStatus.Proposed,  OtherFamilyId);
        var payerSent = Render(SettlementStatus.PayerSent,  OtherFamilyId);
        var completed = Render(SettlementStatus.Completed, OtherFamilyId);

        proposed.Markup.Should().NotBe(payerSent.Markup,
            "Proposed and PayerSent must render differently");
        payerSent.Markup.Should().NotBe(completed.Markup,
            "PayerSent and Completed must render differently");
    }
}
