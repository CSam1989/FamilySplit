using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests.Groups;

// ---------------------------------------------------------------------------
// File-local abstract base — adds group-seeding helpers so every test class
// in this file can share them without duplication. Derives from
// IntegrationTestBase so all base members are accessible via inheritance.
//
// These tests assert the NEW strict-CQRS wire shapes of the migrated Groups
// slice: create → 201 + Location + { id }; update/regenerate/leave → 204;
// join → 200 + { id }. Read endpoints (list, detail) keep their JSON shapes.
// ---------------------------------------------------------------------------

public abstract class GroupTestBase : IntegrationTestBase
{
    protected GroupTestBase(PostgresContainerFixture fixture) : base(fixture) { }

    /// <summary>
    /// Seeds a <c>groups</c> row and a <c>group_families</c> row for the
    /// caller's family with the given role string ("Admin" or "Member").
    /// Returns (groupId, inviteCode).
    /// </summary>
    protected async Task<(Guid groupId, string inviteCode)> SeedGroupWithCallerFamilyAsync(
        string groupName,
        string role,
        CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        var inviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
                VALUES (@id, @name, @code, @createdBy, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", groupName);
            cmd.Parameters.AddWithValue("code", inviteCode);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO group_families (id, group_id, family_id, role, joined_at)
                VALUES (@id, @groupId, @familyId, @role, now())
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            cmd.Parameters.AddWithValue("role", role);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (groupId, inviteCode);
    }

    /// <summary>
    /// Seeds a <c>groups</c> row with a <c>group_families</c> row for a specific
    /// family (not the caller's). Returns the groupId.
    /// </summary>
    protected async Task<Guid> SeedGroupForFamilyAsync(
        string groupName,
        Guid familyId,
        string role,
        CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        var inviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
                VALUES (@id, @name, @code, @createdBy, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", groupName);
            cmd.Parameters.AddWithValue("code", inviteCode);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO group_families (id, group_id, family_id, role, joined_at)
                VALUES (@id, @groupId, @familyId, @role, now())
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", familyId);
            cmd.Parameters.AddWithValue("role", role);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return groupId;
    }

    /// <summary>
    /// Inserts a groups row with no group_families row. Used when the test
    /// itself controls membership via the /groups/join endpoint.
    /// </summary>
    protected async Task<Guid> SeedGroupOnlyAsync(string name, string inviteCode, CancellationToken ct)
    {
        var groupId = Guid.NewGuid();

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
            VALUES (@id, @name, @code, @createdBy, now(), now())
            """;
        cmd.Parameters.AddWithValue("id", groupId);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("code", inviteCode);
        cmd.Parameters.AddWithValue("createdBy", CallerId);
        await cmd.ExecuteNonQueryAsync(ct);

        return groupId;
    }

    /// <summary>Reads the invite code stored against a group directly from the DB.</summary>
    protected async Task<string?> ReadInviteCodeAsync(Guid groupId, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT invite_code FROM groups WHERE id = @id";
        cmd.Parameters.AddWithValue("id", groupId);
        return (string?)await cmd.ExecuteScalarAsync(ct);
    }
}

// ---------------------------------------------------------------------------
// GET /groups
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ListGroupsTests : GroupTestBase
{
    public ListGroupsTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListGroups_WhenCallerBelongsToNoGroups_Returns200EmptyArray()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/groups", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListGroups_WhenCallerIsInGroup_Returns200ContainingThatGroup()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Listed Group", "Admin", ct);

        var response = await Client.GetAsync("/groups", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var groups = doc.RootElement.EnumerateArray().ToList();
        groups.Should().NotBeEmpty();

        var found = groups.FirstOrDefault(g => g.GetProperty("id").GetGuid() == groupId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the seeded group must appear in the caller's group list");
        found.GetProperty("name").GetString().Should().Be("Listed Group");
        found.GetProperty("callerFamilyRole").GetString().Should().Be("Admin");
        found.GetProperty("familyCount").GetInt32().Should().Be(1);
    }
}

// ---------------------------------------------------------------------------
// POST /groups  →  201 Created + Location + { id }
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CreateGroupTests : GroupTestBase
{
    public CreateGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateGroup_ValidRequest_Returns201WithIdAndLocation_ThenGetConfirmsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "Holiday 2026", description = (string?)null });

        // Act — create.
        var response = await Client.PostAsync("/groups", payload, ct);

        // Assert — 201 + Location + { id }.
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetGuid();
        id.Should().NotBeEmpty();
        doc.RootElement.EnumerateObject().Should().ContainSingle("the create response carries only the id");

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be($"/groups/{id}");

        // Follow-up GET confirms the group exists with the caller as the Admin family.
        var detail = await Client.GetAsync($"/groups/{id}", ct);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailBody = await detail.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);
        detailDoc.RootElement.GetProperty("name").GetString().Should().Be("Holiday 2026");
        detailDoc.RootElement.GetProperty("callerFamilyRole").GetString().Should().Be("Admin");

        var families = detailDoc.RootElement.GetProperty("families").EnumerateArray().ToList();
        families.Should().ContainSingle();
        families[0].GetProperty("familyId").GetGuid().Should().Be(CallerFamilyId);
        families[0].GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task CreateGroup_EmptyName_Returns422OnNameField()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "", description = (string?)null });

        var response = await Client.PostAsync("/groups", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Group name is required.");
    }
}

// ---------------------------------------------------------------------------
// GET /groups/{id}
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class GetGroupDetailTests : GroupTestBase
{
    public GetGroupDetailTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetGroupDetail_AsMember_Returns200WithFamilies()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Detail Group", "Admin", ct);

        var response = await Client.GetAsync($"/groups/{groupId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("id").GetGuid().Should().Be(groupId);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Detail Group");

        var families = doc.RootElement.GetProperty("families").EnumerateArray().ToList();
        families.Should().ContainSingle();
        families[0].GetProperty("familyId").GetGuid().Should().Be(CallerFamilyId);
    }

    [Fact]
    public async Task GetGroupDetail_AsNonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (family2Id, _) = await SeedExtraFamilyAsync();
        var groupId = await SeedGroupForFamilyAsync("Non-Member Group", family2Id, "Admin", ct);

        // The default Client (caller's family) is not in this group.
        var response = await Client.GetAsync($"/groups/{groupId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------------
// PUT /groups/{id}  →  204 No Content
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateGroupTests : GroupTestBase
{
    public UpdateGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateGroup_AsAdmin_Returns204_ThenGetShowsNewName()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Original Name", "Admin", ct);

        var payload = JsonContent.Create(new { name = "Renamed Group", description = "New desc" });

        // Act — update.
        var response = await Client.PutAsync($"/groups/{groupId}", payload, ct);

        // Assert — 204, empty body.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        // Re-query confirms the rename persisted.
        var detail = await Client.GetAsync($"/groups/{groupId}", ct);
        var detailBody = await detail.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);
        detailDoc.RootElement.GetProperty("name").GetString().Should().Be("Renamed Group");
        detailDoc.RootElement.GetProperty("description").GetString().Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateGroup_AsMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Member Role Group", "Member", ct);

        var payload = JsonContent.Create(new { name = "Should Fail", description = (string?)null });

        var response = await Client.PutAsync($"/groups/{groupId}", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateGroup_EmptyName_Returns422OnNameField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Original", "Admin", ct);

        var payload = JsonContent.Create(new { name = "", description = (string?)null });

        var response = await Client.PutAsync($"/groups/{groupId}", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Group name is required.");
    }
}

// ---------------------------------------------------------------------------
// POST /groups/join  →  200 OK + { id }
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class JoinGroupTests : GroupTestBase
{
    public JoinGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task JoinGroup_ValidCode_SecondFamilyJoins_Returns200WithGroupId()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviteCode = "ABCD1234";
        var groupId = await SeedGroupOnlyAsync("Join Group", inviteCode, ct);

        // Seed a second family + linked user so we have an authenticated second client.
        // SeedExtraFamilyAsync creates the member with IsAdmin = true, so it passes the
        // family-admin precondition for joining.
        var (family2Id, member2Id) = await SeedExtraFamilyAsync();
        var user2Id = await SeedUserForMemberAsync(member2Id, "user2@integration.test", "User Two", ct);

        using var client2 = CreateClientForUser(user2Id, "user2@integration.test", "User Two");

        var payload = JsonContent.Create(new { inviteCode });

        // Act — second family joins via invite code.
        var response = await client2.PostAsync("/groups/join", payload, ct);

        // Assert — 200 (documented exception, not 201) + { id }, no Location.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Location.Should().BeNull("join returns 200 + id with no Location header");

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("id").GetGuid().Should().Be(groupId);

        // A follow-up detail GET as the second family shows both families.
        var detail = await client2.GetAsync($"/groups/{groupId}", ct);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detail.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);
        var families = detailDoc.RootElement.GetProperty("families").EnumerateArray().ToList();
        families.Should().Contain(f => f.GetProperty("familyId").GetGuid() == family2Id);
    }

    [Fact]
    public async Task JoinGroup_AlreadyMember_Returns422OnInviteCodeField()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviteCode = "ZZZZ9999";
        await SeedGroupOnlyAsync("Already Member Group", inviteCode, ct);

        var payload = JsonContent.Create(new { inviteCode });

        // First join — caller's family has no membership yet, so it should succeed (200).
        var firstJoin = await Client.PostAsync("/groups/join", payload, ct);
        firstJoin.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second join by the same family must be rejected.
        var secondJoin = await Client.PostAsync("/groups/join", payload, ct);
        secondJoin.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await secondJoin.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("InviteCode")
            .EnumerateArray().First().GetString()
            .Should().Be("Your family is already a member of this group.");
    }

    [Fact]
    public async Task JoinGroup_InvalidCode_Returns422OnInviteCodeField()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { inviteCode = "XXXXXXXX" });

        var response = await Client.PostAsync("/groups/join", payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("InviteCode")
            .EnumerateArray().First().GetString()
            .Should().Be("Invite code is invalid or has expired.");
    }

    // -------------------------------------------------------------------------
    // Helpers specific to join tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds a User row and links it to an existing FamilyMember by id.
    /// Returns the new userId.
    /// </summary>
    private async Task<Guid> SeedUserForMemberAsync(
        Guid memberId, string email, string displayName, CancellationToken ct)
    {
        var userId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-join-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("display_name", displayName);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE family_members SET user_id = @userId, email = @email WHERE id = @memberId
                """;
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("memberId", memberId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return userId;
    }

    private HttpClient CreateClientForUser(Guid userId, string email, string displayName)
    {
        var token = JwtHelper.Mint(userId, email, displayName, false, TestSigningKey);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

// ---------------------------------------------------------------------------
// POST /groups/{id}/invite-code  →  204 No Content (new code observed via GET)
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RegenerateInviteCodeTests : GroupTestBase
{
    public RegenerateInviteCodeTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RegenerateInviteCode_AsAdmin_Returns204_ThenGetShowsDifferentCode()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, oldInviteCode) = await SeedGroupWithCallerFamilyAsync("Regen Group", "Admin", ct);

        // Act — regenerate.
        var response = await Client.PostAsync($"/groups/{groupId}/invite-code", null, ct);

        // Assert — 204, empty body.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        // The new code is observed by re-GETting the detail (admin sees the code).
        var detail = await Client.GetAsync($"/groups/{groupId}", ct);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detail.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);

        var newCode = detailDoc.RootElement.GetProperty("inviteCode").GetString();
        newCode.Should().NotBeNullOrEmpty();
        newCode.Should().NotBe(oldInviteCode, "the regenerated invite code must differ from the original");
        newCode!.Length.Should().Be(8);

        // And the DB row reflects the change too.
        (await ReadInviteCodeAsync(groupId, ct)).Should().Be(newCode);
    }

    [Fact]
    public async Task RegenerateInviteCode_AsMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, oldCode) = await SeedGroupWithCallerFamilyAsync("Member Role Regen", "Member", ct);

        var response = await Client.PostAsync($"/groups/{groupId}/invite-code", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The code must be unchanged.
        (await ReadInviteCodeAsync(groupId, ct)).Should().Be(oldCode);
    }
}

// ---------------------------------------------------------------------------
// DELETE /groups/{id}/leave  →  204 No Content
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class LeaveGroupTests : GroupTestBase
{
    public LeaveGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LeaveGroup_AsMember_Returns204_ThenGroupNoLongerListed()
    {
        var ct = TestContext.Current.CancellationToken;

        // Seed a group owned by a second family (so it survives the caller leaving),
        // then add the caller's family as a plain Member.
        var (family2Id, _) = await SeedExtraFamilyAsync();
        var groupId = await SeedGroupForFamilyAsync("Leave Group", family2Id, "Admin", ct);
        await AddCallerFamilyToGroupAsync(groupId, "Member", ct);

        // Act — caller's family leaves.
        var response = await Client.DeleteAsync($"/groups/{groupId}/leave", ct);

        // Assert — 204, empty body.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        // The group no longer appears in the caller's list.
        var list = await Client.GetAsync("/groups", ct);
        var listBody = await list.Content.ReadAsStringAsync(ct);
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.EnumerateArray()
            .Should().NotContain(g => g.GetProperty("id").GetGuid() == groupId);
    }

    [Fact]
    public async Task LeaveGroup_SoleAdmin_Returns422OnGroupField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Sole Admin Group", "Admin", ct);

        var response = await Client.DeleteAsync($"/groups/{groupId}/leave", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Group")
            .EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task LeaveGroup_NotMemberOfGroup_Returns422OnGroupField()
    {
        var ct = TestContext.Current.CancellationToken;
        var (family2Id, _) = await SeedExtraFamilyAsync();
        var groupId = await SeedGroupForFamilyAsync("Other Family Group", family2Id, "Admin", ct);

        // Caller's family is not in this group.
        var response = await Client.DeleteAsync($"/groups/{groupId}/leave", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Group")
            .EnumerateArray().First().GetString()
            .Should().Be("Your family is not a member of this group.");
    }

    /// <summary>Adds the caller's family to an existing group with the given role.</summary>
    private async Task AddCallerFamilyToGroupAsync(Guid groupId, string role, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO group_families (id, group_id, family_id, role, joined_at)
            VALUES (@id, @groupId, @familyId, @role, now())
            """;
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("groupId", groupId);
        cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
        cmd.Parameters.AddWithValue("role", role);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
