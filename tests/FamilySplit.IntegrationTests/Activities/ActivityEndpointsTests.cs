using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Activities;

// ── Create activity ────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CreateActivityTests : IntegrationTestBase
{
    public CreateActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateActivity_ValidRequest_Returns201WithParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var payload = JsonContent.Create(new { name = "Beach Trip", description = "Summer fun" });

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("name").GetString().Should().Be("Beach Trip");
        doc.RootElement.GetProperty("status").GetString().Should().Be("Open");
        doc.RootElement.GetProperty("groupId").GetGuid().Should().Be(groupId);

        // The seeded caller member must appear as participant
        var participants = doc.RootElement.GetProperty("participants");
        participants.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        participants.EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .Should().BeTrue("caller member should be auto-seeded as participant");
    }

    [Fact]
    public async Task CreateActivity_NonMember_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Create a second user+family that has NOT joined the group
        var (outsiderUserId, _) = await SeedOutsiderUserAsync(ct);
        using var outsiderClient = CreateClientForUser(outsiderUserId);

        var payload = JsonContent.Create(new { name = "Gatecrash Trip" });

        // Act
        var response = await outsiderClient.PostAsync($"/groups/{groupId}/activities", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateActivity_EmptyName_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var payload = JsonContent.Create(new { name = "" });

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Activity name is required.");
    }

    // ── Shared helpers ─────────────────────────────────────────────────────────

    private async Task<(Guid userId, Guid memberId)> SeedOutsiderUserAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, @name, now(), now())";
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", "Outsider Family");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-outsider-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "outsider@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Outsider User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", familyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "outsider@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Outsider User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (userId, memberId);
    }

    private HttpClient CreateClientForUser(Guid userId)
    {
        var token = JwtHelper.Mint(
            userId: userId,
            email: "outsider@integration.test",
            displayName: "Outsider User",
            isGlobalAdmin: false,
            signingKey: TestSigningKey);

        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── List activities ────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ListActivitiesTests : IntegrationTestBase
{
    public ListActivitiesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListActivities_Returns200WithCreatedActivity()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "Mountain Hike" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Act
        var listResponse = await Client.GetAsync($"/groups/{groupId}/activities", ct);

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        listDoc.RootElement.EnumerateArray()
            .Any(a => a.GetProperty("id").GetGuid() == activityId)
            .Should().BeTrue("created activity must appear in list");
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Get activity detail ────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class GetActivityDetailTests : IntegrationTestBase
{
    public GetActivityDetailTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetActivityDetail_Returns200WithParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "Camping Weekend" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Act
        var detailResponse = await Client.GetAsync($"/groups/{groupId}/activities/{activityId}", ct);

        // Assert
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailBody = await detailResponse.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);

        detailDoc.RootElement.GetProperty("id").GetGuid().Should().Be(activityId);
        detailDoc.RootElement.GetProperty("name").GetString().Should().Be("Camping Weekend");

        var participants = detailDoc.RootElement.GetProperty("participants");
        participants.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetActivityDetail_NonMember_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "Members Only" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Seed an outsider (a user+family not in the group)
        var outsiderId = await SeedOutsiderUserAsync(ct);
        using var outsiderClient = CreateClientForUser(outsiderId);

        // Act
        var response = await outsiderClient.GetAsync($"/groups/{groupId}/activities/{activityId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedOutsiderUserAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, @name, now(), now())";
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", "Outsider Family Detail");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-out-detail-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "outsiderdetail@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Outsider Detail");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", familyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "outsiderdetail@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Outsider Detail");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return userId;
    }

    private HttpClient CreateClientForUser(Guid userId)
    {
        var token = JwtHelper.Mint(
            userId: userId,
            email: "outsiderdetail@integration.test",
            displayName: "Outsider Detail",
            isGlobalAdmin: false,
            signingKey: TestSigningKey);

        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Update activity ────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateActivityTests : IntegrationTestBase
{
    public UpdateActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateActivity_ValidRequest_Returns200WithUpdatedName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "Original Name" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        var updatePayload = JsonContent.Create(new { name = "Updated Name", description = "New desc" });

        // Act
        var response = await Client.PutAsync($"/groups/{groupId}/activities/{activityId}", updatePayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Updated Name");
        doc.RootElement.GetProperty("description").GetString().Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateActivity_OnClosedActivity_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "To Be Closed" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Close the activity
        var closeResponse = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatePayload = JsonContent.Create(new { name = "Should Fail" });

        // Act
        var response = await Client.PutAsync($"/groups/{groupId}/activities/{activityId}", updatePayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Sub-activities ─────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class SubActivityTests : IntegrationTestBase
{
    public SubActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateSubActivity_ValidParent_Returns201()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var parentPayload = JsonContent.Create(new { name = "Parent Activity" });
        var parentResponse = await Client.PostAsync($"/groups/{groupId}/activities", parentPayload, ct);
        parentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var parentBody = await parentResponse.Content.ReadAsStringAsync(ct);
        using var parentDoc = JsonDocument.Parse(parentBody);
        var parentId = parentDoc.RootElement.GetProperty("id").GetGuid();

        var subPayload = JsonContent.Create(new { name = "Sub Activity" });

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities", subPayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Sub Activity");
        doc.RootElement.GetProperty("parentActivityId").GetGuid().Should().Be(parentId);
    }

    [Fact]
    public async Task CreateSubActivity_OfSubActivity_Returns422_DepthGuard()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Create parent
        var parentPayload = JsonContent.Create(new { name = "Top Level" });
        var parentResponse = await Client.PostAsync($"/groups/{groupId}/activities", parentPayload, ct);
        var parentBody = await parentResponse.Content.ReadAsStringAsync(ct);
        using var parentDoc = JsonDocument.Parse(parentBody);
        var parentId = parentDoc.RootElement.GetProperty("id").GetGuid();

        // Create sub-activity of the parent
        var subPayload = JsonContent.Create(new { name = "Level 2" });
        var subResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities", subPayload, ct);
        subResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var subBody = await subResponse.Content.ReadAsStringAsync(ct);
        using var subDoc = JsonDocument.Parse(subBody);
        var subId = subDoc.RootElement.GetProperty("id").GetGuid();

        var deepPayload = JsonContent.Create(new { name = "Level 3 — should fail" });

        // Act — attempt to nest a sub-activity under a sub-activity
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{subId}/sub-activities", deepPayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Close activity ─────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CloseActivityTests : IntegrationTestBase
{
    public CloseActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CloseActivity_AbsorbsOpenSubActivities()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Create parent activity
        var parentPayload = JsonContent.Create(new { name = "Parent To Close" });
        var parentResponse = await Client.PostAsync($"/groups/{groupId}/activities", parentPayload, ct);
        parentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var parentBody = await parentResponse.Content.ReadAsStringAsync(ct);
        using var parentDoc = JsonDocument.Parse(parentBody);
        var parentId = parentDoc.RootElement.GetProperty("id").GetGuid();

        // Create an open sub-activity
        var subPayload = JsonContent.Create(new { name = "Open Sub" });
        var subResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities", subPayload, ct);
        subResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var subBody = await subResponse.Content.ReadAsStringAsync(ct);
        using var subDoc = JsonDocument.Parse(subBody);
        var subId = subDoc.RootElement.GetProperty("id").GetGuid();

        // Act — close the parent
        var closeResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/close", null, ct);

        // Assert
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var closeBody = await closeResponse.Content.ReadAsStringAsync(ct);
        using var closeDoc = JsonDocument.Parse(closeBody);
        closeDoc.RootElement.GetProperty("status").GetString().Should().Be("Closed");

        // Verify sub is now AbsorbedByParent via detail GET on the parent
        var detailResponse = await Client.GetAsync($"/groups/{groupId}/activities/{parentId}", ct);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailBody = await detailResponse.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);

        var subs = detailDoc.RootElement.GetProperty("subActivities");
        var sub = subs.EnumerateArray().First(s => s.GetProperty("id").GetGuid() == subId);
        sub.GetProperty("status").GetString().Should().Be("AbsorbedByParent");
    }

    [Fact]
    public async Task CloseActivity_AlreadyClosed_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var createPayload = JsonContent.Create(new { name = "Double Close" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // First close — OK
        var firstClose = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);
        firstClose.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — second close attempt
        var secondClose = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        // Assert
        secondClose.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Add participant ────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AddParticipantTests : IntegrationTestBase
{
    public AddParticipantTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddParticipant_MemberFromSecondFamily_Returns200()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Seed a second family that has also joined the group
        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Second Family", "Second Member");
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Member', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", secondFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Create an activity (participants seeded from all group families automatically)
        var createPayload = JsonContent.Create(new { name = "Group Activity" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Remove secondMember from participants first so we can re-add them
        await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);

        // Act — add the second family's member back explicitly
        var addPayload = JsonContent.Create(new { familyMemberId = secondMemberId });
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/participants", addPayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var participants = doc.RootElement.GetProperty("participants");
        participants.EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == secondMemberId)
            .Should().BeTrue("second member should now be in participants");
    }

    [Fact]
    public async Task AddParticipant_OnClosedActivity_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Closed Test Family", "Closed Test Member");
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Member', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", secondFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var createPayload = JsonContent.Create(new { name = "Closed Activity" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Remove then close so we can test re-adding to closed
        await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);
        await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        var addPayload = JsonContent.Create(new { familyMemberId = secondMemberId });

        // Act
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/participants", addPayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}

// ── Remove participant ─────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RemoveParticipantTests : IntegrationTestBase
{
    public RemoveParticipantTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RemoveParticipant_OpenActivity_Returns200AndMemberGone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Remove Test Family", "Remove Test Member");
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Member', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", secondFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Create activity — second member is auto-seeded
        var createPayload = JsonContent.Create(new { name = "Remove Participant Activity" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Act
        var response = await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var participants = doc.RootElement.GetProperty("participants");
        participants.EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == secondMemberId)
            .Should().BeFalse("removed member should no longer be in participants");
    }

    [Fact]
    public async Task RemoveParticipant_ClosedActivity_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Remove Closed Family", "Remove Closed Member");
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Member', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", secondFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var createPayload = JsonContent.Create(new { name = "Closed Remove Activity" });
        var createResponse = await Client.PostAsync($"/groups/{groupId}/activities", createPayload, ct);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var activityId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Close the activity before attempting removal
        await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        // Act
        var response = await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> SeedGroupAsync(CancellationToken ct)
    {
        var groupId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at) VALUES (@id, @name, @code, @createdBy, now(), now())";
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Test Group");
            cmd.Parameters.AddWithValue("code", Guid.NewGuid().ToString("N")[..8]);
            cmd.Parameters.AddWithValue("createdBy", CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Admin', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return groupId;
    }
}
