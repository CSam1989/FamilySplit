using FamilySplit.E2ETests.Infrastructure;
using Npgsql;

namespace FamilySplit.E2ETests.Flows;

/// <summary>
/// Task 4.2e — Permission guards:
///   • Non-admin cannot see rename/remove controls.
///   • Unauthenticated visit redirects to login.
/// </summary>
[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class PermissionGuardTests : E2ETestBase
{
    private readonly E2EApiServer _api;

    public PermissionGuardTests(E2EApiServer api, E2EClientServer client) : base(api, client)
        => _api = api;

    [Fact]
    public async Task UnauthenticatedNavigation_DoesNotLandOnProtectedPage()
    {
        if (!ClientAvailable) return;

        // No cookie set — just navigate to a protected route
        await Page.GotoAsync("/groups");
        await WaitForNetworkIdleAsync(Page);

        // RequireAuth redirects client-side (no network), so wait for the URL to change.
        // The redirect fires after IsLoading transitions false and IsAuthenticated is false.
        await Page.WaitForURLAsync(
            url => !url.EndsWith("/groups", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 5_000 });

        // The app must redirect away from /groups (login page, root, or not-registered)
        var url = Page.Url;
        url.Should().NotEndWith("/groups",
            "unauthenticated user must not land on the groups page");
    }

    [Fact]
    public async Task NonAdminUser_CannotSeeAddMemberButton()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;

        // Seed a non-admin family member with a linked user
        var nonAdminUserId = await SeedNonAdminUserAsync(ct);

        // Authenticate as the non-admin user
        await using var nonAdminPage = await CreatePageForUserAsync(nonAdminUserId);

        await nonAdminPage.GotoAsync("/families/mine");
        await WaitForNetworkIdleAsync(nonAdminPage);

        // The "Add member" button must not be rendered for non-admins
        await Expect(nonAdminPage.Locator("[data-testid='btn-add-member']"))
            .ToBeHiddenAsync();

        await nonAdminPage.CloseAsync();
    }

    [Fact]
    public async Task LoggedInUser_OwnRemoveButton_IsNeverShown()
    {
        if (!ClientAvailable) return;

        await AuthenticateContextAsync();
        await Page.GotoAsync("/families/mine");
        await WaitForNetworkIdleAsync(Page);

        // The caller's own remove button must never be rendered,
        // even when they are an admin.
        var ownRemoveBtn = Page.Locator(
            $"[data-testid='btn-remove-member-{TestMemberId}']");
        await Expect(ownRemoveBtn).ToBeHiddenAsync();
    }

    [Fact]
    public async Task NonGroupMember_CannotNavigateToGroupDetail()
    {
        if (!ClientAvailable) return;

        var ct = TestContext.Current.CancellationToken;

        // Seed a group that the caller's family is NOT in
        var outsiderGroupId = await SeedGroupForOtherFamilyAsync(ct);

        await AuthenticateContextAsync();
        await Page.GotoAsync($"/groups/{outsiderGroupId}");
        await WaitForNetworkIdleAsync(Page);

        // The app should show an error or redirect — not render the group detail.
        // At minimum the group name must not be visible; there may be an error banner.
        // text= is a Playwright pseudo-selector and cannot be mixed with CSS in a single Locator call.
        var errorVisible =
            await Page.Locator(".mud-alert").First.IsVisibleAsync()
            || await Page.Locator(".mud-snackbar").First.IsVisibleAsync()
            || await Page.GetByText("Forbidden").First.IsVisibleAsync()
            || await Page.GetByText("403").First.IsVisibleAsync();
        var redirected = !Page.Url.Contains(outsiderGroupId.ToString());

        (errorVisible || redirected).Should().BeTrue(
            "a non-member navigating to a group detail must see an error or be redirected");
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    private async Task<Guid> SeedNonAdminUserAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @ext, 'Google', @email, 'Non-Admin User', false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("ext", "google-nonadmin-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("email", $"nonadmin-{userId:N}@test.example");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members
                    (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @fid, @uid, @email, 'Non-Admin User', false, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("fid", TestFamilyId);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("email", $"nonadmin-{userId:N}@test.example");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return userId;
    }

    private async Task<Guid> SeedGroupForOtherFamilyAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_api.DbConnectionString);
        await conn.OpenAsync(ct);

        var otherFamilyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, 'Other Family', now(), now())";
            cmd.Parameters.AddWithValue("id", otherFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
                VALUES (@id, 'Private Group', @code, @uid, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("uid", TestUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @gid, @fid, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("gid", groupId);
            cmd.Parameters.AddWithValue("fid", otherFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return groupId;
    }

    // CreatePageForUserAsync is inherited from E2ETestBase.
}
