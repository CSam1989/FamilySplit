using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests.Activities;

// ---------------------------------------------------------------------------
// File-local base — group/activity seeding helpers shared by every test class.
//
// These tests assert the NEW strict-CQRS wire shapes of the migrated Activities
// slice: create / sub-create → 201 + Location + { id }; update / close /
// add-participant / remove-participant → 204. Read endpoints (list, detail)
// keep their JSON shapes (the read-side wire-format lock).
// ---------------------------------------------------------------------------

public abstract class ActivityTestBase : IntegrationTestBase
{
    protected ActivityTestBase(PostgresContainerFixture fixture) : base(fixture) { }

    /// <summary>Seeds a group with the caller's family as Admin. Returns the groupId.</summary>
    protected async Task<Guid> SeedGroupAsync(CancellationToken ct)
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

    /// <summary>Adds an existing family to a group with the given role.</summary>
    protected async Task AddFamilyToGroupAsync(Guid groupId, Guid familyId, string role, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, @role, now())";
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("groupId", groupId);
        cmd.Parameters.AddWithValue("familyId", familyId);
        cmd.Parameters.AddWithValue("role", role);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Adds a FamilyMember (no User) to an existing family. Returns the memberId.</summary>
    protected async Task<Guid> AddMemberAsync(Guid familyId, string displayName, bool isActive, CancellationToken ct)
    {
        var memberId = Guid.NewGuid();
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
            VALUES (@id, @family_id, @display_name, false, @is_active, now())
            """;
        cmd.Parameters.AddWithValue("id", memberId);
        cmd.Parameters.AddWithValue("family_id", familyId);
        cmd.Parameters.AddWithValue("display_name", displayName);
        cmd.Parameters.AddWithValue("is_active", isActive);
        await cmd.ExecuteNonQueryAsync(ct);
        return memberId;
    }

    /// <summary>POSTs a top-level activity and returns the new id from the 201 + { id } body.</summary>
    protected async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var response = await Client.PostAsync($"/groups/{groupId}/activities", JsonContent.Create(new { name }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>GETs the activity detail document (caller is authorized).</summary>
    protected async Task<JsonDocument> GetActivityDetailAsync(Guid groupId, Guid activityId, CancellationToken ct)
    {
        var response = await Client.GetAsync($"/groups/{groupId}/activities/{activityId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(body);
    }

    protected async Task<(Guid userId, Guid memberId)> SeedOutsiderUserAsync(string slug, CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var email = $"outsider-{slug}@integration.test";

        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO families (id, name, created_at, updated_at) VALUES (@id, @name, now(), now())";
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", "Outsider Family " + slug);
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
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("display_name", "Outsider " + slug);
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
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("display_name", "Outsider " + slug);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return (userId, memberId);
    }

    protected HttpClient CreateClientForUser(Guid userId, string slug)
    {
        var token = JwtHelper.Mint(userId, $"outsider-{slug}@integration.test", "Outsider " + slug, false, TestSigningKey);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

// ── Create activity → 201 + Location + { id } ──────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CreateActivityTests : ActivityTestBase
{
    public CreateActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateActivity_ValidRequest_Returns201WithIdAndLocation_ThenGetConfirmsParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Act — create.
        var response = await Client.PostAsync($"/groups/{groupId}/activities",
            JsonContent.Create(new { name = "Beach Trip", description = "Summer fun" }), ct);

        // Assert — 201 + Location + { id }.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetGuid();
        id.Should().NotBeEmpty();
        doc.RootElement.EnumerateObject().Should().ContainSingle("the create response carries only the id");
        response.Headers.Location!.ToString().Should().Be($"/groups/{groupId}/activities/{id}");

        // Follow-up GET confirms the activity exists with the caller auto-seeded as participant.
        using var detail = await GetActivityDetailAsync(groupId, id, ct);
        detail.RootElement.GetProperty("name").GetString().Should().Be("Beach Trip");
        detail.RootElement.GetProperty("status").GetString().Should().Be("Open");
        detail.RootElement.GetProperty("groupId").GetGuid().Should().Be(groupId);
        detail.RootElement.GetProperty("participants").EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .Should().BeTrue("caller member should be auto-seeded as participant");
    }

    [Fact]
    public async Task CreateActivity_SeedsActiveMembersOnly_AcrossGroupFamilies()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // A second family in the group with one active and one inactive member.
        var (family2Id, member2Id) = await SeedExtraFamilyAsync("Seed Family", "Active Two");
        await AddFamilyToGroupAsync(groupId, family2Id, "Member", ct);
        var inactiveId = await AddMemberAsync(family2Id, "Inactive", isActive: false, ct);

        var activityId = await CreateActivityAsync(groupId, "Seeding Activity", ct);

        using var detail = await GetActivityDetailAsync(groupId, activityId, ct);
        var participantIds = detail.RootElement.GetProperty("participants").EnumerateArray()
            .Select(p => p.GetProperty("familyMemberId").GetGuid()).ToList();

        participantIds.Should().Contain(CallerMemberId);
        participantIds.Should().Contain(member2Id);
        participantIds.Should().NotContain(inactiveId, "inactive members are not seeded as participants");
    }

    [Fact]
    public async Task CreateActivity_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (outsiderUserId, _) = await SeedOutsiderUserAsync("create", ct);
        using var outsiderClient = CreateClientForUser(outsiderUserId, "create");

        var response = await outsiderClient.PostAsync($"/groups/{groupId}/activities",
            JsonContent.Create(new { name = "Gatecrash Trip" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateActivity_EmptyName_Returns422()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var response = await Client.PostAsync($"/groups/{groupId}/activities",
            JsonContent.Create(new { name = "" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Name")
            .EnumerateArray().First().GetString()
            .Should().Be("Activity name is required.");
    }
}

// ── List activities ────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ListActivitiesTests : ActivityTestBase
{
    public ListActivitiesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListActivities_Returns200WithCreatedActivity()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Mountain Hike", ct);

        var listResponse = await Client.GetAsync($"/groups/{groupId}/activities", ct);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        listDoc.RootElement.EnumerateArray()
            .Any(a => a.GetProperty("id").GetGuid() == activityId)
            .Should().BeTrue("created activity must appear in list");
    }
}

// ── Get activity detail ────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class GetActivityDetailTests : ActivityTestBase
{
    public GetActivityDetailTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetActivityDetail_Returns200WithParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Camping Weekend", ct);

        using var detail = await GetActivityDetailAsync(groupId, activityId, ct);

        detail.RootElement.GetProperty("id").GetGuid().Should().Be(activityId);
        detail.RootElement.GetProperty("name").GetString().Should().Be("Camping Weekend");
        detail.RootElement.GetProperty("participants").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetActivityDetail_NonMember_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Members Only", ct);

        var (outsiderId, _) = await SeedOutsiderUserAsync("detail", ct);
        using var outsiderClient = CreateClientForUser(outsiderId, "detail");

        var response = await outsiderClient.GetAsync($"/groups/{groupId}/activities/{activityId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

// ── Update activity → 204 No Content ────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateActivityTests : ActivityTestBase
{
    public UpdateActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateActivity_ValidRequest_Returns204_ThenGetShowsUpdatedName()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Original Name", ct);

        var response = await Client.PutAsync($"/groups/{groupId}/activities/{activityId}",
            JsonContent.Create(new { name = "Updated Name", description = "New desc" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        using var detail = await GetActivityDetailAsync(groupId, activityId, ct);
        detail.RootElement.GetProperty("name").GetString().Should().Be("Updated Name");
        detail.RootElement.GetProperty("description").GetString().Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateActivity_OnClosedActivity_Returns422()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "To Be Closed", ct);

        var closeResponse = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);
        closeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Client.PutAsync($"/groups/{groupId}/activities/{activityId}",
            JsonContent.Create(new { name = "Should Fail" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

// ── Sub-activities → 201 + { id } ────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class SubActivityTests : ActivityTestBase
{
    public SubActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateSubActivity_ValidParent_Returns201_ThenGetShowsParentAndCopiedParticipants()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var parentId = await CreateActivityAsync(groupId, "Parent Activity", ct);

        var response = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities",
            JsonContent.Create(new { name = "Sub Activity" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var subId = doc.RootElement.GetProperty("id").GetGuid();
        subId.Should().NotBeEmpty();
        doc.RootElement.EnumerateObject().Should().ContainSingle("the create response carries only the id");
        response.Headers.Location!.ToString().Should().Be($"/groups/{groupId}/activities/{subId}");

        using var subDetail = await GetActivityDetailAsync(groupId, subId, ct);
        subDetail.RootElement.GetProperty("name").GetString().Should().Be("Sub Activity");
        subDetail.RootElement.GetProperty("parentActivityId").GetGuid().Should().Be(parentId);

        // The sub copies the parent's participants (the caller).
        subDetail.RootElement.GetProperty("participants").EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .Should().BeTrue("sub-activity copies the parent's participants");
    }

    [Fact]
    public async Task CreateSubActivity_OfSubActivity_Returns422_DepthGuard()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var parentId = await CreateActivityAsync(groupId, "Top Level", ct);

        var subResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities",
            JsonContent.Create(new { name = "Level 2" }), ct);
        subResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var subBody = await subResponse.Content.ReadAsStringAsync(ct);
        using var subDoc = JsonDocument.Parse(subBody);
        var subId = subDoc.RootElement.GetProperty("id").GetGuid();

        // Attempt to nest a sub-activity under a sub-activity.
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{subId}/sub-activities",
            JsonContent.Create(new { name = "Level 3 — should fail" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

// ── Close activity → 204 No Content ──────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class CloseActivityTests : ActivityTestBase
{
    public CloseActivityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CloseActivity_Returns204_AndAbsorbsOpenSubActivities()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var parentId = await CreateActivityAsync(groupId, "Parent To Close", ct);

        var subResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/sub-activities",
            JsonContent.Create(new { name = "Open Sub" }), ct);
        subResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var subBody = await subResponse.Content.ReadAsStringAsync(ct);
        using var subDoc = JsonDocument.Parse(subBody);
        var subId = subDoc.RootElement.GetProperty("id").GetGuid();

        // Act — close the parent.
        var closeResponse = await Client.PostAsync($"/groups/{groupId}/activities/{parentId}/close", null, ct);

        // Assert — 204, empty body.
        closeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await closeResponse.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        // Re-query the parent: it is Closed and the sub is AbsorbedByParent.
        using var detail = await GetActivityDetailAsync(groupId, parentId, ct);
        detail.RootElement.GetProperty("status").GetString().Should().Be("Closed");
        var sub = detail.RootElement.GetProperty("subActivities").EnumerateArray()
            .First(s => s.GetProperty("id").GetGuid() == subId);
        sub.GetProperty("status").GetString().Should().Be("AbsorbedByParent");
    }

    [Fact]
    public async Task CloseActivity_AlreadyClosed_Returns422()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Double Close", ct);

        var firstClose = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);
        firstClose.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondClose = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        secondClose.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

// ── Add participant → 204 No Content ─────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AddParticipantTests : ActivityTestBase
{
    public AddParticipantTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AddParticipant_MemberFromSecondFamily_Returns204_ThenGetShowsMember()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Second Family", "Second Member");
        await AddFamilyToGroupAsync(groupId, secondFamilyId, "Member", ct);

        // Create the activity (participants seeded from all group families automatically).
        var activityId = await CreateActivityAsync(groupId, "Group Activity", ct);

        // Remove secondMember first so we can re-add them.
        var removeResponse = await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — add the second family's member back explicitly.
        var response = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/participants",
            JsonContent.Create(new { familyMemberId = secondMemberId }), ct);

        // Assert — 204, then GET shows the member.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        using var detail = await GetActivityDetailAsync(groupId, activityId, ct);
        detail.RootElement.GetProperty("participants").EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == secondMemberId)
            .Should().BeTrue("second member should now be in participants");
    }

    [Fact]
    public async Task AddParticipant_OnClosedActivity_Returns422()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Closed Test Family", "Closed Test Member");
        await AddFamilyToGroupAsync(groupId, secondFamilyId, "Member", ct);

        var activityId = await CreateActivityAsync(groupId, "Closed Activity", ct);

        // Remove then close so we can test re-adding to a closed activity.
        await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);
        await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        var response = await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/participants",
            JsonContent.Create(new { familyMemberId = secondMemberId }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

// ── Remove participant → 204 No Content ──────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RemoveParticipantTests : ActivityTestBase
{
    public RemoveParticipantTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task RemoveParticipant_OpenActivity_Returns204_AndMemberGone()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Remove Test Family", "Remove Test Member");
        await AddFamilyToGroupAsync(groupId, secondFamilyId, "Member", ct);

        var activityId = await CreateActivityAsync(groupId, "Remove Participant Activity", ct);

        var response = await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        using var detail = await GetActivityDetailAsync(groupId, activityId, ct);
        detail.RootElement.GetProperty("participants").EnumerateArray()
            .Any(p => p.GetProperty("familyMemberId").GetGuid() == secondMemberId)
            .Should().BeFalse("removed member should no longer be in participants");
    }

    [Fact]
    public async Task RemoveParticipant_ClosedActivity_Returns422()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Remove Closed Family", "Remove Closed Member");
        await AddFamilyToGroupAsync(groupId, secondFamilyId, "Member", ct);

        var activityId = await CreateActivityAsync(groupId, "Closed Remove Activity", ct);

        await Client.PostAsync($"/groups/{groupId}/activities/{activityId}/close", null, ct);

        var response = await Client.DeleteAsync($"/groups/{groupId}/activities/{activityId}/participants/{secondMemberId}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
