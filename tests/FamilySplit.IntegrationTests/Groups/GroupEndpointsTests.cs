using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Groups;

// ---------------------------------------------------------------------------
// File-local abstract base — adds group-seeding helpers so every test class
// in this file can share them without duplication. Derives from
// IntegrationTestBase so all base members are accessible via inheritance.
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
        // Arrange — no groups seeded; caller's family is not in any group.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await Client.GetAsync("/groups", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListGroups_WhenCallerIsInGroup_Returns200ContainingThatGroup()
    {
        // Arrange — seed a group and put the caller's family in it.
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Listed Group", "Admin", ct);

        // Act
        var response = await Client.GetAsync("/groups", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var groups = doc.RootElement.EnumerateArray().ToList();
        groups.Should().NotBeEmpty();

        var found = groups.FirstOrDefault(g => g.GetProperty("id").GetGuid() == groupId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the seeded group must appear in the caller's group list");
        found.GetProperty("name").GetString().Should().Be("Listed Group");
    }
}

// ---------------------------------------------------------------------------
// POST /groups
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CreateGroupTests : GroupTestBase
{
    public CreateGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateGroup_ValidRequest_Returns201WithGroupDetail()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "Holiday 2026", description = (string?)null });

        // Act
        var response = await Client.PostAsync("/groups", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("name").GetString().Should().Be("Holiday 2026");
        doc.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();

        // The caller's family must be listed as the Admin GroupFamily.
        var families = doc.RootElement.GetProperty("families").EnumerateArray().ToList();
        families.Should().ContainSingle();
        families[0].GetProperty("familyId").GetGuid().Should().Be(CallerFamilyId);
        families[0].GetProperty("role").GetString().Should().Be("Admin");
    }

    [Fact]
    public async Task CreateGroup_EmptyName_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "", description = (string?)null });

        // Act
        var response = await Client.PostAsync("/groups", payload, ct);

        // Assert
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
        // Arrange — seed a group where the caller's family is a member.
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Detail Group", "Admin", ct);

        // Act
        var response = await Client.GetAsync($"/groups/{groupId}", ct);

        // Assert
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
        // Arrange — seed a group owned by a second family; caller's family is not in it.
        var ct = TestContext.Current.CancellationToken;
        var (family2Id, _) = await SeedExtraFamilyAsync();
        var groupId = await SeedGroupForFamilyAsync("Non-Member Group", family2Id, "Admin", ct);

        // Act — the default Client (caller's family) is not in this group.
        var response = await Client.GetAsync($"/groups/{groupId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------------
// PUT /groups/{id}
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateGroupTests : GroupTestBase
{
    public UpdateGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateGroup_AsAdmin_Returns200WithUpdatedName()
    {
        // Arrange — seed a group where the caller's family has role Admin.
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Original Name", "Admin", ct);

        var payload = JsonContent.Create(new { name = "Renamed Group", description = (string?)null });

        // Act
        var response = await Client.PutAsync($"/groups/{groupId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Renamed Group");
    }

    [Fact]
    public async Task UpdateGroup_AsMember_Returns403()
    {
        // Arrange — seed a group where the caller's family has role Member (not Admin).
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Member Role Group", "Member", ct);

        var payload = JsonContent.Create(new { name = "Should Fail", description = (string?)null });

        // Act
        var response = await Client.PutAsync($"/groups/{groupId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------------
// POST /groups/join
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class JoinGroupTests : GroupTestBase
{
    public JoinGroupTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task JoinGroup_ValidCode_SecondFamilyJoins_Returns200()
    {
        // Arrange — seed a group with a known invite code (no group_families row yet).
        var ct = TestContext.Current.CancellationToken;
        var inviteCode = "ABCD1234";
        var groupId = await SeedGroupOnlyAsync("Join Group", inviteCode, ct);

        // Seed a second family + linked user so we have an authenticated second client.
        var (family2Id, member2Id) = await SeedExtraFamilyAsync();
        var user2Id = await SeedUserForMemberAsync(member2Id, "user2@integration.test", "User Two", ct);

        using var client2 = CreateClientForUser(user2Id, "user2@integration.test", "User Two");

        var payload = JsonContent.Create(new { inviteCode });

        // Act — second family joins via invite code.
        var response = await client2.PostAsync("/groups/join", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("id").GetGuid().Should().Be(groupId);

        // The second family must now appear among the group's families.
        var families = doc.RootElement.GetProperty("families").EnumerateArray().ToList();
        families.Should().Contain(f => f.GetProperty("familyId").GetGuid() == family2Id);
    }

    [Fact]
    public async Task JoinGroup_AlreadyMember_Returns422()
    {
        // Arrange — caller's family joins once, then tries again.
        var ct = TestContext.Current.CancellationToken;
        var inviteCode = "ZZZZ9999";
        await SeedGroupOnlyAsync("Already Member Group", inviteCode, ct);

        var payload = JsonContent.Create(new { inviteCode });

        // First join — caller's family has no membership yet, so it should succeed.
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
    public async Task JoinGroup_InvalidCode_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { inviteCode = "XXXXXXXX" });

        // Act
        var response = await Client.PostAsync("/groups/join", payload, ct);

        // Assert
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
    /// Inserts a groups row with no group_families row. Used when the test
    /// itself controls membership via the /groups/join endpoint.
    /// </summary>
    private async Task<Guid> SeedGroupOnlyAsync(
        string name, string inviteCode, CancellationToken ct)
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
// POST /groups/{id}/invite-code
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RegenerateInviteCodeTests : GroupTestBase
{
    public RegenerateInviteCodeTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RegenerateInviteCode_AsAdmin_Returns200WithNewCodeDifferentFromOld()
    {
        // Arrange — seed a group where the caller's family is Admin.
        var ct = TestContext.Current.CancellationToken;
        var (groupId, oldInviteCode) = await SeedGroupWithCallerFamilyAsync("Regen Group", "Admin", ct);

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/invite-code", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var newCode = doc.RootElement.GetProperty("inviteCode").GetString();
        newCode.Should().NotBeNullOrEmpty();
        newCode.Should().NotBe(oldInviteCode,
            "the regenerated invite code must differ from the original");
        newCode!.Length.Should().Be(8);
    }

    [Fact]
    public async Task RegenerateInviteCode_AsMember_Returns403()
    {
        // Arrange — caller's family has role Member, not Admin.
        var ct = TestContext.Current.CancellationToken;
        var (groupId, _) = await SeedGroupWithCallerFamilyAsync("Member Role Regen", "Member", ct);

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/invite-code", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
