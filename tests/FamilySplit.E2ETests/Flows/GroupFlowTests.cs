using FamilySplit.E2ETests.Infrastructure;
using Npgsql;

namespace FamilySplit.E2ETests.Flows;

/// <summary>Task 4.2a — Group create + join via invite code.</summary>
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class GroupFlowTests : E2ETestBase
{
    private readonly E2EApiServer _api;

    public GroupFlowTests(E2EApiServer api, E2EClientServer client) : base(api, client)
        => _api = api;

    [Fact]
    public async Task CreateGroup_AppearsInGroupList()
    {
        if (!ClientAvailable) return;

        await AuthenticateContextAsync();
        await Page.GotoAsync("/groups");
        await Page.WaitForSelectorAsync("[data-testid='btn-create-group']");

        // Open Create group dialog
        await Page.ClickAsync("[data-testid='btn-create-group']");
        await Page.WaitForSelectorAsync("[data-testid='input-group-name']");

        await Page.FillAsync("[data-testid='input-group-name']", "Holiday 2026");
        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // After creation the app navigates to the new group's detail page.
        // Navigate back to the list and verify the card is present.
        await Page.WaitForURLAsync(
            url => url.Contains("/groups/"),
            new PageWaitForURLOptions { Timeout = 10_000 });
        await Page.GotoAsync("/groups");
        await Expect(Page.Locator("[data-group-name='Holiday 2026']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SecondFamily_JoinsViaInviteCode_BothFamiliesAppearInGroupDetail()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;

        // ── Seed: create a group as the first user (caller) ──────────────────
        await AuthenticateContextAsync();
        await Page.GotoAsync("/groups");
        await Page.WaitForSelectorAsync("[data-testid='btn-create-group']");
        await Page.ClickAsync("[data-testid='btn-create-group']");
        await Page.WaitForSelectorAsync("[data-testid='input-group-name']");
        await Page.FillAsync("[data-testid='input-group-name']", "Joint Trip");
        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // After creation the app navigates directly to the group detail page.
        // Expand the families panel so the invite code becomes visible.
        await Page.WaitForURLAsync(
            url => url.Contains("/groups/"),
            new PageWaitForURLOptions { Timeout = 10_000 });
        await Page.ClickAsync("[data-testid='families-expansion-panel'] .mud-expand-panel-header");
        await Page.WaitForSelectorAsync("[data-testid='invite-code-value']");

        var inviteCode = await Page.TextContentAsync("[data-testid='invite-code-value']");
        inviteCode = inviteCode?.Trim() ?? throw new InvalidOperationException("Invite code not found.");

        // ── Second family joins ───────────────────────────────────────────────
        var (family2Id, _) = await SeedExtraFamilyWithUserAsync("Second Family", ct);
        await using var client2 = await CreateAuthenticatedPageAsync(family2Id);

        await client2.GotoAsync("/groups");
        await client2.WaitForSelectorAsync("[data-testid='btn-join-group']");
        await client2.ClickAsync("[data-testid='btn-join-group']");
        await client2.WaitForSelectorAsync("[data-testid='input-invite-code']");

        await client2.FillAsync("[data-testid='input-invite-code']", inviteCode);
        await client2.ClickAsync("[data-testid='btn-dialog-submit']");

        // After joining the app navigates directly to the group detail page.
        await client2.WaitForURLAsync(
            url => url.Contains("/groups/"),
            new PageWaitForURLOptions { Timeout = 10_000 });

        // Both family names must appear in the group detail
        await Expect(client2.Locator("text=E2E Test Family")).ToBeVisibleAsync();
        await Expect(client2.Locator("text=Second Family")).ToBeVisibleAsync();

        await client2.CloseAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid familyId, Guid memberId)> SeedExtraFamilyWithUserAsync(
        string familyName, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO families (id, name, created_at, updated_at)
                VALUES (@id, @name, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", familyName);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @ext, 'Google', @email, @name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("ext", "google-e2e-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("email", $"e2e-{userId:N}@test.example");
            cmd.Parameters.AddWithValue("name", familyName + " User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members
                    (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @fid, @uid, @email, @name, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("fid", familyId);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("email", $"e2e-{userId:N}@test.example");
            cmd.Parameters.AddWithValue("name", familyName + " User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (familyId, memberId);
    }

    private async Task<IPage> CreateAuthenticatedPageAsync(Guid familyId)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync();

        Guid userId;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT user_id FROM family_members WHERE family_id = @fid AND user_id IS NOT NULL LIMIT 1";
            cmd.Parameters.AddWithValue("fid", familyId);
            userId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        // Use the base-class helper so token seeding logic lives in one place.
        return await CreatePageForUserAsync(userId);
    }
}
