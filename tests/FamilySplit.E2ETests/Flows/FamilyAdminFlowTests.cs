using FamilySplit.E2ETests.Infrastructure;
using Npgsql;

namespace FamilySplit.E2ETests.Flows;

/// <summary>Task 4.2d — Family admin: add member → appears; remove member → gone.</summary>
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class FamilyAdminFlowTests : E2ETestBase
{
    private readonly E2EApiServer _api;

    public FamilyAdminFlowTests(E2EApiServer api, E2EClientServer client) : base(api, client)
        => _api = api;

    [Fact]
    public async Task AddMember_AppearsInMemberTable()
    {
        if (!ClientAvailable) return;

        await AuthenticateContextAsync();
        await Page.GotoAsync("/families/mine");
        await Page.WaitForSelectorAsync("[data-testid='btn-add-member']");

        await Page.ClickAsync("[data-testid='btn-add-member']");
        await Page.WaitForSelectorAsync("[data-testid='input-member-name']");

        await Page.FillAsync("[data-testid='input-member-name']", "New Child");
        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // The new member must appear in the table
        await Expect(Page.Locator("text=New Child")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RemoveMember_DisappearsFromMemberTable()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;

        // Seed an extra member so we have someone to remove
        var extraMemberId = await SeedExtraMemberAsync("Extra Member", ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync("/families/mine");

        // Wait for the remove button for the seeded member. It renders only after
        // the caller's profile has loaded (admin guard), so allow for a cold WASM
        // boot — the default 5s Expect timeout flakes here.
        var removeBtn = Page.Locator($"[data-testid='btn-remove-member-{extraMemberId}']");
        await Expect(removeBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await removeBtn.ClickAsync();

        // Confirm removal in MudMessageBox (yesText = "Remove")
        await Page.WaitForSelectorAsync(".mud-message-box");
        await Page.ClickAsync(".mud-message-box button:has-text('Remove')");

        // Member row must disappear
        await Expect(Page.Locator("text=Extra Member")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task AdminUser_CanSeeAddAndRemoveControls()
    {
        if (!ClientAvailable) return;

        await AuthenticateContextAsync();
        await Page.GotoAsync("/families/mine");
        await Page.WaitForSelectorAsync("[data-testid='btn-add-member']");

        // Add member button is visible for an admin
        await Expect(Page.Locator("[data-testid='btn-add-member']")).ToBeVisibleAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedExtraMemberAsync(string name, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var memberId = Guid.NewGuid();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
            VALUES (@id, @fid, @name, false, true, now())
            """;
        cmd.Parameters.AddWithValue("id", memberId);
        cmd.Parameters.AddWithValue("fid", TestFamilyId);
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync(ct);

        return memberId;
    }
}
