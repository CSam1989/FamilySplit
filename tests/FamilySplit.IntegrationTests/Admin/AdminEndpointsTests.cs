using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Admin;

// ---------------------------------------------------------------------------
// File-local abstract base — adds the global-admin promotion helper so every
// test class in this file can call PromoteToGlobalAdminAsync without
// duplicating code. It derives from IntegrationTestBase so all base members
// (Connection, CallerId, etc.) are accessible via inheritance.
// ---------------------------------------------------------------------------

public abstract class AdminTestBase : IntegrationTestBase
{
    protected AdminTestBase(PostgresContainerFixture fixture) : base(fixture) { }

    /// <summary>
    /// Promotes the seeded caller User to global admin directly in the DB.
    /// </summary>
    protected async Task PromoteToGlobalAdminAsync(CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET is_global_admin = true WHERE id = @id";
        cmd.Parameters.AddWithValue("id", CallerId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Seeds a passive (non-admin, no user) FamilyMember in the caller's family.
    /// </summary>
    protected async Task<Guid> SeedPassiveMemberAsync(CancellationToken ct)
    {
        var memberId = Guid.NewGuid();

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
            VALUES (@id, @family_id, @display_name, false, true, now())
            """;
        cmd.Parameters.AddWithValue("id", memberId);
        cmd.Parameters.AddWithValue("family_id", CallerFamilyId);
        cmd.Parameters.AddWithValue("display_name", "Passive Admin Test Member");
        await cmd.ExecuteNonQueryAsync(ct);

        return memberId;
    }
}

// ---------------------------------------------------------------------------
// GET /admin/families
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminListFamiliesTests : AdminTestBase
{
    public AdminListFamiliesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListFamilies_NonGlobalAdmin_Returns403()
    {
        // Arrange — Client is seeded as a non-global-admin by default.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await Client.GetAsync("/admin/families", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListFamilies_GlobalAdmin_Returns200WithSeededFamily()
    {
        // Arrange — promote the seeded caller to global admin.
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        // Act
        var response = await Client.GetAsync("/admin/families", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        var families = doc.RootElement.EnumerateArray().ToList();
        families.Should().NotBeEmpty();

        var seededFamily = families.FirstOrDefault(f =>
            f.GetProperty("id").GetGuid() == CallerFamilyId);
        seededFamily.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "the seeded integration-test family must appear in the admin list");
        seededFamily.GetProperty("name").GetString().Should().Be("Integration Test Family");
    }
}

// ---------------------------------------------------------------------------
// POST /admin/families
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminCreateFamilyTests : AdminTestBase
{
    public AdminCreateFamilyTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateFamily_ValidRequest_Returns201WithFamilyName()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        var payload = JsonContent.Create(new { name = "Admin Created Family" });

        // Act
        var response = await Client.PostAsync("/admin/families", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Admin Created Family");
        doc.RootElement.GetProperty("id").GetGuid().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateFamily_EmptyName_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        var payload = JsonContent.Create(new { name = "" });

        // Act
        var response = await Client.PostAsync("/admin/families", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Family name is required.");
    }
}

// ---------------------------------------------------------------------------
// GET /admin/families/{familyId}
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminGetFamilyTests : AdminTestBase
{
    public AdminGetFamilyTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetFamily_GlobalAdmin_Returns200WithMembersArray()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        // Act
        var response = await Client.GetAsync($"/admin/families/{CallerFamilyId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("id").GetGuid().Should().Be(CallerFamilyId);
        doc.RootElement.GetProperty("name").GetString().Should().Be("Integration Test Family");

        var members = doc.RootElement.GetProperty("members");
        members.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var callerMember = members.EnumerateArray()
            .FirstOrDefault(m => m.GetProperty("id").GetGuid() == CallerMemberId);
        callerMember.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        callerMember.GetProperty("displayName").GetString().Should().Be("Integration Test User");
    }

    [Fact]
    public async Task GetFamily_NonGlobalAdmin_Returns403()
    {
        // Arrange — default Client is NOT a global admin.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await Client.GetAsync($"/admin/families/{CallerFamilyId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------------
// POST /admin/families/{familyId}/members
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminAddFamilyMemberTests : AdminTestBase
{
    public AdminAddFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddMember_ValidRequest_Returns201AndMemberAppearsInGetFamily()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        var payload = JsonContent.Create(new
        {
            displayName = "New Admin Member",
            email = "newadminmember@integration.test",
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await Client.PostAsync(
            $"/admin/families/{CallerFamilyId}/members", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var newMemberId = doc.RootElement.GetProperty("id").GetGuid();
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("New Admin Member");

        // Verify the member appears in GET /admin/families/{id}
        var getResponse = await Client.GetAsync($"/admin/families/{CallerFamilyId}", ct);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadAsStringAsync(ct);
        using var getDoc = JsonDocument.Parse(getBody);
        var members = getDoc.RootElement.GetProperty("members");
        members.EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == newMemberId)
            .Should().BeTrue("newly added member must appear in the family detail");
    }
}

// ---------------------------------------------------------------------------
// PUT /admin/families/{familyId}/members/{memberId}
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminUpdateFamilyMemberTests : AdminTestBase
{
    public AdminUpdateFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateMember_GlobalAdmin_Returns200WithUpdatedFields()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        var payload = JsonContent.Create(new
        {
            displayName = "Updated By Admin",
            email = "testuser@integration.test",
            dateOfBirth = "2000-06-15",
            weightOverride = (decimal?)null,
            isAdmin = true
        });

        // Act
        var response = await Client.PutAsync(
            $"/admin/families/{CallerFamilyId}/members/{CallerMemberId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("displayName").GetString().Should().Be("Updated By Admin");
        doc.RootElement.GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("dateOfBirth").GetString().Should().Be("2000-06-15");
    }

    [Fact]
    public async Task UpdateMember_NonGlobalAdmin_Returns403()
    {
        // Arrange — default Client is NOT a global admin.
        var ct = TestContext.Current.CancellationToken;

        var payload = JsonContent.Create(new
        {
            displayName = "Should Fail",
            email = "testuser@integration.test",
            dateOfBirth = (string?)null,
            weightOverride = (decimal?)null,
            isAdmin = false
        });

        // Act
        var response = await Client.PutAsync(
            $"/admin/families/{CallerFamilyId}/members/{CallerMemberId}", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ---------------------------------------------------------------------------
// DELETE /admin/families/{familyId}/members/{memberId}
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AdminRemoveFamilyMemberTests : AdminTestBase
{
    public AdminRemoveFamilyMemberTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RemoveMember_GlobalAdmin_Returns204AndMemberAbsentFromGetFamily()
    {
        // Arrange — seed an extra passive member to delete (avoids touching the
        // caller's own member which has a linked User account).
        var ct = TestContext.Current.CancellationToken;
        await PromoteToGlobalAdminAsync(ct);

        var targetMemberId = await SeedPassiveMemberAsync(ct);

        // Confirm member is present before delete
        var beforeResponse = await Client.GetAsync($"/admin/families/{CallerFamilyId}", ct);
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeBody = await beforeResponse.Content.ReadAsStringAsync(ct);
        using var beforeDoc = JsonDocument.Parse(beforeBody);
        beforeDoc.RootElement.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == targetMemberId)
            .Should().BeTrue("member must exist before deletion");

        // Act
        var response = await Client.DeleteAsync(
            $"/admin/families/{CallerFamilyId}/members/{targetMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Member must be absent from subsequent GET (soft-deleted, IsActive = false)
        var afterResponse = await Client.GetAsync($"/admin/families/{CallerFamilyId}", ct);
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterBody = await afterResponse.Content.ReadAsStringAsync(ct);
        using var afterDoc = JsonDocument.Parse(afterBody);
        afterDoc.RootElement.GetProperty("members").EnumerateArray()
            .Any(m => m.GetProperty("id").GetGuid() == targetMemberId)
            .Should().BeFalse("soft-deleted member must not appear in the family detail");
    }

    [Fact]
    public async Task RemoveMember_NonGlobalAdmin_Returns403()
    {
        // Arrange — default Client is NOT a global admin.
        var ct = TestContext.Current.CancellationToken;

        var targetMemberId = await SeedPassiveMemberAsync(ct);

        // Act
        var response = await Client.DeleteAsync(
            $"/admin/families/{CallerFamilyId}/members/{targetMemberId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
