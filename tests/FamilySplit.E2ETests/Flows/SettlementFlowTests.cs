using FamilySplit.E2ETests.Infrastructure;
using Npgsql;

namespace FamilySplit.E2ETests.Flows;

/// <summary>
/// Task 4.2c — Close activity → settlements auto-generate → Mark sent → Mark received → Settled.
/// </summary>
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class SettlementFlowTests : E2ETestBase
{
    private readonly E2EApiServer _api;

    public SettlementFlowTests(E2EApiServer api, E2EClientServer client) : base(api, client)
        => _api = api;

    [Fact]
    public async Task CloseActivity_TransitionsStatusChipToClosed()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;
        var (groupId, activityId) = await SeedOpenActivityAsync("Trip", ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{groupId}/activities/{activityId}");
        await Page.WaitForSelectorAsync("[data-testid='activity-status']");

        await Expect(Page.Locator("[data-testid='activity-status']"))
            .ToContainTextAsync("Open");

        // Click close → MudMessageBox appears (yesText = "Close")
        await Page.ClickAsync("[data-testid='btn-close-activity']");
        await Page.WaitForSelectorAsync(".mud-message-box");
        await Page.ClickAsync(".mud-message-box button:has-text('Close')");

        await Expect(Page.Locator("[data-testid='activity-status']"))
            .ToContainTextAsync("Closed");
    }

    [Fact]
    public async Task FullSettlementLifecycle_PayerSendsReceiverConfirms_ActivitySettled()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;
        var (groupId, activityId, receiver) = await SeedSettlementScenarioAsync(ct);

        // ── Payer (caller) marks payment sent ─────────────────────────────────
        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{groupId}/activities/{activityId}");
        await WaitForNetworkIdleAsync(Page);

        // Settlements are auto-generated when a Closed activity page loads.
        var markSentBtn = Page.Locator("[data-testid^='btn-mark-sent-']").First;
        await Expect(markSentBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

        var testId = await markSentBtn.GetAttributeAsync("data-testid") ?? "";
        var settlementId = testId.Replace("btn-mark-sent-", "");

        await markSentBtn.ClickAsync();
        await Page.WaitForSelectorAsync(".mud-message-box");
        await Page.ClickAsync(".mud-message-box button:has-text('Mark sent')");

        // Status chip shows "Sent"
        await Expect(Page.Locator($"[data-testid='settlement-status-{settlementId}']"))
            .ToContainTextAsync("Sent");

        // ── Receiver confirms receipt ─────────────────────────────────────────
        await using var receiverPage = await CreatePageForUserAsync(receiver.UserId);
        await receiverPage.GotoAsync($"/groups/{groupId}/activities/{activityId}");
        await WaitForNetworkIdleAsync(receiverPage);

        var markRecvBtn = receiverPage.Locator("[data-testid^='btn-mark-received-']").First;
        await Expect(markRecvBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await markRecvBtn.ClickAsync();

        await receiverPage.WaitForSelectorAsync(".mud-message-box");
        await receiverPage.ClickAsync(".mud-message-box button:has-text('Mark received')");

        // All settlements Completed → activity becomes Settled
        await Expect(receiverPage.Locator("[data-testid='activity-status']"))
            .ToContainTextAsync("Settled", new() { Timeout = 15_000 });
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<(Guid groupId, Guid activityId)> SeedOpenActivityAsync(
        string activityName, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        await Exec(conn, ct,
            "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @uid, now(), now())",
            ("id", groupId), ("name", "Test Group"), ("code", RandomCode()), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Admin', now())",
            ("id", Guid.NewGuid()), ("gid", groupId), ("fid", TestFamilyId));

        await Exec(conn, ct,
            "INSERT INTO activities (id, group_id, name, status, created_by_user_id, created_at, updated_at) VALUES (@id, @gid, @name, 'Open', @uid, now(), now())",
            ("id", activityId), ("gid", groupId), ("name", activityName), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO activity_participants (id, activity_id, family_member_id) VALUES (@id, @aid, @mid)",
            ("id", Guid.NewGuid()), ("aid", activityId), ("mid", TestMemberId));

        return (groupId, activityId);
    }

    private sealed record ReceiverSeed(Guid UserId, Guid FamilyId, Guid MemberId);

    /// <summary>
    /// Seeds: group (caller Admin + second family Member), closed activity, expense split 50/50.
    /// Returns (groupId, activityId, receiver info).
    /// </summary>
    private async Task<(Guid groupId, Guid activityId, ReceiverSeed receiver)>
        SeedSettlementScenarioAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var family2Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        // Group + caller as Admin
        await Exec(conn, ct,
            "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, 'Settlement Test', @code, @uid, now(), now())",
            ("id", groupId), ("code", RandomCode()), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Admin', now())",
            ("id", Guid.NewGuid()), ("gid", groupId), ("fid", TestFamilyId));

        // Receiver family + user
        await Exec(conn, ct,
            "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, 'Receiver Family', now(), now())",
            ("id", family2Id));

        await Exec(conn, ct,
            "INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at) VALUES (@id, @ext, 'Google', @email, 'Receiver', false, now())",
            ("id", user2Id), ("ext", "recv-" + user2Id.ToString("N")),
            ("email", $"recv-{user2Id:N}@test.example"));

        await Exec(conn, ct,
            "INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at) VALUES (@id, @fid, @uid, @email, 'Receiver', true, true, now())",
            ("id", member2Id), ("fid", family2Id), ("uid", user2Id),
            ("email", $"recv-{user2Id:N}@test.example"));

        await Exec(conn, ct,
            "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Member', now())",
            ("id", Guid.NewGuid()), ("gid", groupId), ("fid", family2Id));

        // Closed activity
        await Exec(conn, ct,
            "INSERT INTO activities (id, group_id, name, status, created_by_user_id, closed_at, created_at, updated_at) VALUES (@id, @gid, 'Shared Dinner', 'Closed', @uid, now(), now(), now())",
            ("id", activityId), ("gid", groupId), ("uid", TestUserId));

        // Participants (both members, equal weight)
        await Exec(conn, ct,
            "INSERT INTO activity_participants (id, activity_id, family_member_id) VALUES (@id, @aid, @mid)",
            ("id", Guid.NewGuid()), ("aid", activityId), ("mid", TestMemberId));

        await Exec(conn, ct,
            "INSERT INTO activity_participants (id, activity_id, family_member_id) VALUES (@id, @aid, @mid)",
            ("id", Guid.NewGuid()), ("aid", activityId), ("mid", member2Id));

        // Expense paid by caller (€100), split evenly → each family owes €50
        await Exec(conn, ct,
            "INSERT INTO expenses (id, activity_id, title, total_amount, expense_date, paid_by_member_id, currency, created_at, updated_at) VALUES (@id, @aid, 'Dinner', 100, now(), @mid, 'EUR', now(), now())",
            ("id", expenseId), ("aid", activityId), ("mid", TestMemberId));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, family_id, weight_snapshot, share_amount, is_excluded, created_at) VALUES (@id, @eid, @mid, @fid, 1.0, 50, false, now())",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", TestMemberId), ("fid", TestFamilyId));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, family_id, weight_snapshot, share_amount, is_excluded, created_at) VALUES (@id, @eid, @mid, @fid, 1.0, 50, false, now())",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", member2Id), ("fid", family2Id));

        return (groupId, activityId, new ReceiverSeed(user2Id, family2Id, member2Id));
    }

    // CreatePageForUserAsync is inherited from E2ETestBase.

    private static string RandomCode() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task Exec(NpgsqlConnection conn, CancellationToken ct,
        string sql, params (string name, object val)[] ps)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, val) in ps)
            cmd.Parameters.AddWithValue(name, val);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
