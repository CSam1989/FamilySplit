using System.Net.Http.Json;
using System.Text;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Families;

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class GetMyFamilyTests : IntegrationTestBase
{
    public GetMyFamilyTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetMyFamily_ReturnsOk_WithSeededMember()
    {
        // Act
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/families/mine", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("id").GetGuid().Should().Be(CallerFamilyId);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Integration Test Family");

        var members = doc.RootElement.GetProperty("members");
        members.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // The seeded caller member must be present
        var callerMember = members.EnumerateArray()
            .FirstOrDefault(m => m.GetProperty("id").GetGuid() == CallerMemberId);
        callerMember.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        callerMember.GetProperty("displayName").GetString().Should().Be("Integration Test User");
        callerMember.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetMyFamily_Unauthenticated_Returns401()
    {
        // Arrange
        using var anonClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        // Act
        var response = await anonClient.GetAsync("/families/mine", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateFamilyNameTests : IntegrationTestBase
{
    public UpdateFamilyNameTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateFamilyName_AdminCaller_Returns200WithNewName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "Renamed Family" });

        // Act
        var response = await Client.PutAsync("/families/mine", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Renamed Family");
    }

    [Fact]
    public async Task UpdateFamilyName_NonAdminCaller_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonAdminClient = await CreateNonAdminClientAsync(ct);

        var payload = JsonContent.Create(new { name = "Should Fail" });

        // Act
        var response = await nonAdminClient.PutAsync("/families/mine", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateFamilyName_EmptyName_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new { name = "" });

        // Act
        var response = await Client.PutAsync("/families/mine", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Family name is required.");
    }

    private async Task<HttpClient> CreateNonAdminClientAsync(CancellationToken ct)
    {
        var (nonAdminUserId, _) = await SeedNonAdminUserAsync(ct);
        var token = JwtHelper.Mint(
            userId: nonAdminUserId,
            email: "nonadmin@integration.test",
            displayName: "Non-Admin User",
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

    private async Task<(Guid userId, Guid memberId)> SeedNonAdminUserAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-nonadmin-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "nonadmin@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Non-Admin User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, false, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "nonadmin@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Non-Admin User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (userId, memberId);
    }
}

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AddFamilyMemberTests : IntegrationTestBase
{
    public AddFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddMember_AdminCaller_Returns201WithNewMember()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new
        {
            displayName = "New Child",
            email = (string?)null,
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await Client.PostAsync("/families/mine/members", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("New Child");
        doc.RootElement.GetProperty("isAdmin").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AddMember_NonAdminCaller_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonAdminClient = await CreateNonAdminClientAsync(ct);

        var payload = JsonContent.Create(new
        {
            displayName = "Blocked Member",
            email = (string?)null,
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await nonAdminClient.PostAsync("/families/mine/members", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddMember_InvalidEmail_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var payload = JsonContent.Create(new
        {
            displayName = "Bad Email Member",
            email = "not-an-email",
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await Client.PostAsync("/families/mine/members", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Email")
            .EnumerateArray().First().GetString()
            .Should().Be("Email must be a valid email address.");
    }

    private async Task<HttpClient> CreateNonAdminClientAsync(CancellationToken ct)
    {
        var (nonAdminUserId, _) = await SeedNonAdminUserAsync(ct);
        var token = JwtHelper.Mint(
            userId: nonAdminUserId,
            email: "nonadmin2@integration.test",
            displayName: "Non-Admin User 2",
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

    private async Task<(Guid userId, Guid memberId)> SeedNonAdminUserAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-nonadmin2-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "nonadmin2@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Non-Admin User 2");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, false, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "nonadmin2@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Non-Admin User 2");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (userId, memberId);
    }
}

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateFamilyMemberTests : IntegrationTestBase
{
    public UpdateFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateMember_AdminUpdatesAnyMember_Returns200()
    {
        // Arrange — seed a second (non-admin) member to update
        var ct = TestContext.Current.CancellationToken;
        var targetMemberId = await SeedPassiveMemberAsync(ct);

        var payload = JsonContent.Create(new
        {
            displayName = "Updated Name",
            email = (string?)null,
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await Client.PutAsync($"/families/mine/members/{targetMemberId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateMember_NonAdminUpdatesSelf_Returns200()
    {
        // Arrange — seed a non-admin user/member and let them update themselves
        var ct = TestContext.Current.CancellationToken;
        var (nonAdminUserId, nonAdminMemberId) = await SeedNonAdminUserAsync(ct);

        var token = JwtHelper.Mint(
            userId: nonAdminUserId,
            email: "selfupdate@integration.test",
            displayName: "Self Update User",
            isGlobalAdmin: false,
            signingKey: TestSigningKey);

        using var nonAdminClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        nonAdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = JsonContent.Create(new
        {
            displayName = "Self Updated",
            email = "selfupdate@integration.test",
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await nonAdminClient.PutAsync($"/families/mine/members/{nonAdminMemberId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Self Updated");
    }

    [Fact]
    public async Task UpdateMember_NonAdminUpdatesOtherMember_Returns403()
    {
        // Arrange — seed a non-admin user and a separate target member
        var ct = TestContext.Current.CancellationToken;
        var (nonAdminUserId, _) = await SeedNonAdminUserAsync(ct);
        var targetMemberId = await SeedPassiveMemberAsync(ct);

        var token = JwtHelper.Mint(
            userId: nonAdminUserId,
            email: "selfupdate@integration.test",
            displayName: "Self Update User",
            isGlobalAdmin: false,
            signingKey: TestSigningKey);

        using var nonAdminClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        nonAdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = JsonContent.Create(new
        {
            displayName = "Hijacked Name",
            email = (string?)null,
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act — non-admin tries to update a different member
        var response = await nonAdminClient.PutAsync($"/families/mine/members/{targetMemberId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> SeedPassiveMemberAsync(CancellationToken ct)
    {
        var memberId = Guid.NewGuid();

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
            VALUES (@id, @family_id, @display_name, false, true, now())
            """;
        cmd.Parameters.AddWithValue("id", memberId);
        cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
        cmd.Parameters.AddWithValue("display_name", "Passive Member");
        await cmd.ExecuteNonQueryAsync(ct);

        return memberId;
    }

    private async Task<(Guid userId, Guid memberId)> SeedNonAdminUserAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-selfupdate-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "selfupdate@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Self Update User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, false, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "selfupdate@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Self Update User");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (userId, memberId);
    }
}

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RemoveFamilyMemberTests : IntegrationTestBase
{
    public RemoveFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RemoveMember_AdminRemovesNonSelfMember_Returns204()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var targetMemberId = await SeedPassiveMemberAsync(ct);

        // Act
        var response = await Client.DeleteAsync($"/families/mine/members/{targetMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RemoveMember_AdminRemovesSelf_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Act — caller tries to remove their own member record
        var response = await Client.DeleteAsync($"/families/mine/members/{CallerMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("MemberId")
            .EnumerateArray().First().GetString()
            .Should().Be("You cannot remove yourself from the family.");
    }

    [Fact]
    public async Task RemoveMember_SoftDeletedMemberAbsentFromGetMyFamily()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var targetMemberId = await SeedPassiveMemberAsync(ct);

        // Verify the member is present before deletion
        var beforeResponse = await Client.GetAsync("/families/mine", ct);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(ct);
        using var beforeDoc = JsonDocument.Parse(beforeBody);
        var beforeMembers = beforeDoc.RootElement.GetProperty("members");
        beforeMembers.EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == targetMemberId)
            .Should().BeTrue("member should be visible before soft-delete");

        // Act — soft-delete the member
        var deleteResponse = await Client.DeleteAsync($"/families/mine/members/{targetMemberId}", ct);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — member must no longer appear in GET /families/mine
        var afterResponse = await Client.GetAsync("/families/mine", ct);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(ct);
        using var afterDoc = JsonDocument.Parse(afterBody);
        var afterMembers = afterDoc.RootElement.GetProperty("members");
        afterMembers.EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == targetMemberId)
            .Should().BeFalse("soft-deleted member must be excluded from GET /families/mine");
    }

    private async Task<Guid> SeedPassiveMemberAsync(CancellationToken ct)
    {
        var memberId = Guid.NewGuid();

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
            VALUES (@id, @family_id, @display_name, false, true, now())
            """;
        cmd.Parameters.AddWithValue("id", memberId);
        cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
        cmd.Parameters.AddWithValue("display_name", "Passive Member");
        await cmd.ExecuteNonQueryAsync(ct);

        return memberId;
    }
}
