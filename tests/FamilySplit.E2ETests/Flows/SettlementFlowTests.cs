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
        // Must seed a genuine cross-family balance: ActivityEffects.HandleClose auto-generates
        // settlements immediately after close, and an all-zero balance (e.g. a single family with
        // no expenses) is marked Settled immediately by GenerateSettlementsCommandHandler — which
        // would race this test's "Closed" assertion straight past to "Settled".
        var (groupId, activityId) = await SeedOpenActivityWithImbalanceAsync("Trip", ct);

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

        // This activity is seeded directly as Closed via SQL, bypassing the close-activity UI
        // flow (ActivityEffects.HandleClose) that auto-generates settlements. Generation is
        // intentionally never auto-fired merely from viewing a Closed activity (that used to
        // double-dispatch and race into duplicate settlements — see ActivityDetail.razor), so a
        // Closed activity with no settlement rows yet shows a manual "Generate transfers" button.
        await Page.ClickAsync("[data-testid='btn-generate-transfers']");
        await WaitForNetworkIdleAsync(Page);

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

    /// <summary>
    /// Seeds an Open activity shared by two families with a real cross-family expense
    /// imbalance, so closing it produces non-zero settlement transfers (the activity stays
    /// Closed rather than being marked Settled immediately by the zero-balance short-circuit
    /// in GenerateSettlementsCommandHandler).
    /// </summary>
    private async Task<(Guid groupId, Guid activityId)> SeedOpenActivityWithImbalanceAsync(
        string activityName, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var groupId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var family2Id = Guid.NewGuid();
        var member2Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        await Exec(conn, ct,
            "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @uid, now(), now())",
            ("id", groupId), ("name", "Test Group"), ("code", RandomCode()), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Admin', now())",
            ("id", Guid.NewGuid()), ("gid", groupId), ("fid", TestFamilyId));

        // Second family + user so the activity spans two families.
        await Exec(conn, ct,
            "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, 'Second Family', now(), now())",
            ("id", family2Id));

        await Exec(conn, ct,
            "INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at) VALUES (@id, @ext, 'Google', @email, 'Second', false, now())",
            ("id", user2Id), ("ext", "second-" + user2Id.ToString("N")),
            ("email", $"second-{user2Id:N}@test.example"));

        await Exec(conn, ct,
            "INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at) VALUES (@id, @fid, @uid, @email, 'Second', true, true, now())",
            ("id", member2Id), ("fid", family2Id), ("uid", user2Id),
            ("email", $"second-{user2Id:N}@test.example"));

        await Exec(conn, ct,
            "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Member', now())",
            ("id", Guid.NewGuid()), ("gid", groupId), ("fid", family2Id));

        await Exec(conn, ct,
            "INSERT INTO activities (id, group_id, name, status, created_by_user_id, created_at, updated_at) VALUES (@id, @gid, @name, 'Open', @uid, now(), now())",
            ("id", activityId), ("gid", groupId), ("name", activityName), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO activity_participants (id, activity_id, family_member_id) VALUES (@id, @aid, @mid)",
            ("id", Guid.NewGuid()), ("aid", activityId), ("mid", TestMemberId));

        await Exec(conn, ct,
            "INSERT INTO activity_participants (id, activity_id, family_member_id) VALUES (@id, @aid, @mid)",
            ("id", Guid.NewGuid()), ("aid", activityId), ("mid", member2Id));

        // Expense paid by the caller (€100), split evenly → the second family owes €50,
        // giving a genuine non-zero balance once the activity is closed.
        await Exec(conn, ct,
            "INSERT INTO expenses (id, activity_id, title, total_amount, expense_date, paid_by_user_id, currency, status, created_at, updated_at) VALUES (@id, @aid, 'Fuel', 100, now(), @uid, 'EUR', 'Active', now(), now())",
            ("id", expenseId), ("aid", activityId), ("uid", TestUserId));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, weight_snapshot, calculated_amount, is_excluded) VALUES (@id, @eid, @mid, 1.0, 50, false)",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", TestMemberId));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, weight_snapshot, calculated_amount, is_excluded) VALUES (@id, @eid, @mid, 1.0, 50, false)",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", member2Id));

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

        // Expense paid by the second family's user (€100), split evenly → the
        // caller's family owes €50 and is the settlement payer; the second
        // family ("Receiver Family") receives the transfer.
        await Exec(conn, ct,
            "INSERT INTO expenses (id, activity_id, title, total_amount, expense_date, paid_by_user_id, currency, status, created_at, updated_at) VALUES (@id, @aid, 'Dinner', 100, now(), @uid, 'EUR', 'Active', now(), now())",
            ("id", expenseId), ("aid", activityId), ("uid", user2Id));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, weight_snapshot, calculated_amount, is_excluded) VALUES (@id, @eid, @mid, 1.0, 50, false)",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", TestMemberId));

        await Exec(conn, ct,
            "INSERT INTO expense_participants (id, expense_id, family_member_id, weight_snapshot, calculated_amount, is_excluded) VALUES (@id, @eid, @mid, 1.0, 50, false)",
            ("id", Guid.NewGuid()), ("eid", expenseId), ("mid", member2Id));

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
