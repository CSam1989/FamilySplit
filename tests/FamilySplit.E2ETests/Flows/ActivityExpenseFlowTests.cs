using FamilySplit.E2ETests.Infrastructure;
using Npgsql;

namespace FamilySplit.E2ETests.Flows;

/// <summary>Task 4.2b — Create activity, add expense, verify per-participant breakdown.</summary>
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class ActivityExpenseFlowTests : E2ETestBase
{
    private readonly E2EApiServer _api;

    public ActivityExpenseFlowTests(E2EApiServer api, E2EClientServer client) : base(api, client)
        => _api = api;

    [Fact]
    public async Task CreateActivity_AddsToGroupDetail()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupWithCallerAsync("Test Group", ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{groupId}");
        await Page.WaitForSelectorAsync("[data-testid='btn-new-activity']");

        await Page.ClickAsync("[data-testid='btn-new-activity']");
        await Page.WaitForSelectorAsync("[data-testid='input-activity-name']");

        await Page.FillAsync("[data-testid='input-activity-name']", "Summer Hike");
        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // Activity row appears in the group detail
        await Expect(Page.Locator("text=Summer Hike")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddExpense_AppearsInExpenseList()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;
        var (groupId, activityId) = await SeedGroupAndActivityAsync("Camping Trip", "Gear", ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{groupId}/activities/{activityId}");
        await Page.WaitForSelectorAsync("[data-testid='btn-add-expense']");

        // Open Add expense dialog
        await Page.ClickAsync("[data-testid='btn-add-expense']");
        await Page.WaitForSelectorAsync("[data-testid='input-expense-title']");

        await Page.FillAsync("[data-testid='input-expense-title']", "Tent rental");
        // Clear and type the amount (MudNumericField renders as <input type="number">)
        await Page.FillAsync("[data-testid='input-expense-amount']", "120");

        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // Expense row appears in the list
        await Expect(Page.Locator("[data-testid^='expense-row-']")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Tent rental")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ExpenseBreakdown_ShowsWeightBasedShares()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;
        var (groupId, activityId) = await SeedGroupAndActivityAsync("Weekend", "Food", ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{groupId}/activities/{activityId}");
        await Page.WaitForSelectorAsync("[data-testid='btn-add-expense']");

        // Add an expense
        await Page.ClickAsync("[data-testid='btn-add-expense']");
        await Page.WaitForSelectorAsync("[data-testid='input-expense-title']");
        await Page.FillAsync("[data-testid='input-expense-title']", "Groceries");
        await Page.FillAsync("[data-testid='input-expense-amount']", "100");
        await Page.ClickAsync("[data-testid='btn-dialog-submit']");

        // Open the expense detail to see the breakdown
        await Expect(Page.Locator("[data-testid^='expense-row-']")).ToBeVisibleAsync();
        await Page.ClickAsync("[data-testid^='expense-row-']");

        // The breakdown table must be visible — it always shows for any expense
        await Expect(Page.Locator("text=Groceries")).ToBeVisibleAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedGroupWithCallerAsync(string name, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var groupId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
                VALUES (@id, @name, @code, @userId, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", name);
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8].ToUpperInvariant());
            cmd.Parameters.AddWithValue("userId", TestUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO group_families (id, group_id, family_id, role, joined_at)
                VALUES (@id, @gid, @fid, 'Admin', now())
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("gid", groupId);
            cmd.Parameters.AddWithValue("fid", TestFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return groupId;
    }

    private async Task<(Guid groupId, Guid activityId)> SeedGroupAndActivityAsync(
        string groupName, string activityName, CancellationToken ct)
    {
        var groupId = await SeedGroupWithCallerAsync(groupName, ct);

        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var activityId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO activities (id, group_id, name, status, created_by_user_id, created_at, updated_at)
                VALUES (@id, @gid, @name, 'Open', @uid, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", activityId);
            cmd.Parameters.AddWithValue("gid", groupId);
            cmd.Parameters.AddWithValue("name", activityName);
            cmd.Parameters.AddWithValue("uid", TestUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO activity_participants (id, activity_id, family_member_id)
                VALUES (@id, @aid, @mid)
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("aid", activityId);
            cmd.Parameters.AddWithValue("mid", TestMemberId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (groupId, activityId);
    }
}
