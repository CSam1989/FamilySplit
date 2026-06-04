using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests.Settlements;

// ---------------------------------------------------------------------------
// Shared scenario helper record
// ---------------------------------------------------------------------------

record SettlementScenario(
    Guid GroupId,
    Guid ActivityId,
    Guid Family2Id,
    Guid Member2Id,
    Guid User2Id,
    HttpClient Client2);

// ---------------------------------------------------------------------------
// GET /groups/{groupId}/activities/{activityId}/balances
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class BalancesTests : IntegrationTestBase
{
    public BalancesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetBalances_ClosedActivityWithExpense_ReturnsCorrectSignsAndZeroSum()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SetupSettlementScenarioAsync(ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/balances";

        // Act
        var response = await Client.GetAsync(url, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var arr = doc.RootElement.EnumerateArray().ToList();

        arr.Should().HaveCount(2, "two families participated in the expense");

        var sum = arr.Sum(el => el.GetProperty("balance").GetDecimal());
        sum.Should().BeApproximately(0m, 0.01m, "balances must net to zero");

        // Caller's family paid €100 for two participants — positive balance (creditor)
        var callerBalance = arr.FirstOrDefault(el =>
            el.GetProperty("familyId").GetGuid() == CallerFamilyId);
        callerBalance.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        callerBalance.GetProperty("balance").GetDecimal().Should().BePositive(
            "the family that paid the expense is owed money");

        // Second family — negative balance (debtor)
        var family2Balance = arr.FirstOrDefault(el =>
            el.GetProperty("familyId").GetGuid() == scenario.Family2Id);
        family2Balance.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        family2Balance.GetProperty("balance").GetDecimal().Should().BeNegative(
            "the family that did not pay owes money");
    }

    [Fact]
    public async Task GetBalances_NonMemberCaller_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SetupSettlementScenarioAsync(ct);

        // Create an outsider client — a user in a completely different family
        var (_, outsiderClient) = await CreateOutsiderClientAsync(ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/balances";

        // Act
        var response = await outsiderClient.GetAsync(url, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid userId, HttpClient client)> CreateOutsiderClientAsync(CancellationToken ct)
    {
        var (outsiderFamilyId, _) = await SeedExtraFamilyAsync("Outsider Family", "Outsider Member");

        var outsiderUserId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @ext, 'Google', @email, 'Outsider', false, now())
                """;
            cmd.Parameters.AddWithValue("id", outsiderUserId);
            cmd.Parameters.AddWithValue("ext", "google-outsider-" + outsiderUserId.ToString("N"));
            cmd.Parameters.AddWithValue("email", "outsider@integration.test");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Seed a family member linked to the outsider user (in outsiderFamily)
        var outsiderMemberId = Guid.NewGuid();
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @fid, @uid, 'outsider@integration.test', 'Outsider', true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", outsiderMemberId);
            cmd.Parameters.AddWithValue("fid", outsiderFamilyId);
            cmd.Parameters.AddWithValue("uid", outsiderUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        var token = JwtHelper.Mint(outsiderUserId, "outsider@integration.test", "Outsider",
            false, TestSigningKey);
        var outsiderClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        outsiderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (outsiderUserId, outsiderClient);
    }

    // -------------------------------------------------------------------------
    // Scenario factory — reused by all test classes via the base helper
    // -------------------------------------------------------------------------
    private async Task<SettlementScenario> SetupSettlementScenarioAsync(CancellationToken ct)
        => await SettlementScenarioHelper.SetupAsync(this, ct);
}

// ---------------------------------------------------------------------------
// POST /groups/{groupId}/activities/{activityId}/settlements (generate)
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class GenerateSettlementsTests : IntegrationTestBase
{
    public GenerateSettlementsTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Generate_ClosedActivityWithImbalance_Returns200WithOneSettlement()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements";

        // Act
        var response = await Client.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var arr = doc.RootElement.EnumerateArray().ToList();

        // 2 families → at most 1 settlement (N-1)
        arr.Should().HaveCount(1, "two families need exactly one transfer to settle");
        arr[0].GetProperty("payerFamilyId").GetGuid().Should().Be(scenario.Family2Id,
            "family2 has a negative balance — they are the payer");
        arr[0].GetProperty("receiverFamilyId").GetGuid().Should().Be(CallerFamilyId,
            "caller's family paid and is owed money — they are the receiver");
        arr[0].GetProperty("amount").GetDecimal().Should().BePositive();
        arr[0].GetProperty("status").GetString().Should().Be("Proposed");
    }

    [Fact]
    public async Task Generate_CalledTwice_IsIdempotentAndNoDuplicates()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements";

        // Act — generate twice
        var response1 = await Client.PostAsync(url, null, ct);
        var response2 = await Client.PostAsync(url, null, ct);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var body1 = await response1.Content.ReadAsStringAsync(ct);
        var body2 = await response2.Content.ReadAsStringAsync(ct);

        using var doc1 = System.Text.Json.JsonDocument.Parse(body1);
        using var doc2 = System.Text.Json.JsonDocument.Parse(body2);

        var ids1 = doc1.RootElement.EnumerateArray()
            .Select(el => el.GetProperty("id").GetGuid())
            .OrderBy(id => id)
            .ToList();
        var ids2 = doc2.RootElement.EnumerateArray()
            .Select(el => el.GetProperty("id").GetGuid())
            .OrderBy(id => id)
            .ToList();

        ids1.Should().BeEquivalentTo(ids2, "idempotent call must return the same settlement IDs");
        ids1.Should().HaveCount(1, "no duplicate rows should be created");
    }

    [Fact]
    public async Task Generate_ZeroBalanceActivity_Returns200EmptyListAndActivityIsSettled()
    {
        // Arrange — create a group + activity with no expenses, then close it
        var ct = TestContext.Current.CancellationToken;
        var (_, groupId) = await SettlementScenarioHelper.SeedGroupWithBothFamiliesAsync(this, ct);

        // Create activity (auto-seeds participants from both families)
        var createActivityResponse = await Client.PostAsync(
            $"/groups/{groupId}/activities",
            JsonContent.Create(new { name = "Zero Balance Activity", description = (string?)null }),
            ct);
        createActivityResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var activityBody = await createActivityResponse.Content.ReadAsStringAsync(ct);
        using var activityDoc = System.Text.Json.JsonDocument.Parse(activityBody);
        var activityId = activityDoc.RootElement.GetProperty("id").GetGuid();

        // Close without adding any expenses
        var closeResponse = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/close", null, ct);
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var url = $"/groups/{groupId}/activities/{activityId}/settlements";

        // Act
        var response = await Client.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        doc.RootElement.EnumerateArray().Should().BeEmpty(
            "no expenses means zero balances — nothing to settle");

        // Verify activity is now Settled
        var activityResponse = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}", ct);
        activityResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activityDetailBody = await activityResponse.Content.ReadAsStringAsync(ct);
        using var activityDetailDoc = System.Text.Json.JsonDocument.Parse(activityDetailBody);
        activityDetailDoc.RootElement.GetProperty("status").GetString().Should().Be("Settled");
    }
}

// ---------------------------------------------------------------------------
// POST /settlements/{settlementId}/confirm-sent
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ConfirmSentTests : IntegrationTestBase
{
    public ConfirmSentTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ConfirmSent_PayerFamily_Returns200AndStatusIsPayerSent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateAndGetSettlementIdAsync(scenario, ct);

        // Family2 is the payer — use client2
        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-sent";

        // Act
        var response = await scenario.Client2.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("PayerSent");
        doc.RootElement.GetProperty("approvalSteps").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("approvalSteps")[0]
            .GetProperty("stepType").GetString().Should().Be("PayerSent");
    }

    [Fact]
    public async Task ConfirmSent_ReceiverFamilyTriesToConfirmSent_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateAndGetSettlementIdAsync(scenario, ct);

        // Caller is the receiver family — they should NOT be able to confirm sent
        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-sent";

        // Act — caller (receiver family) tries confirm-sent
        var response = await Client.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConfirmSent_AlreadyPayerSent_Returns422()
    {
        // Arrange — advance the settlement to PayerSent first
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateAndGetSettlementIdAsync(scenario, ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-sent";

        // First confirm-sent succeeds
        var first = await scenario.Client2.PostAsync(url, null, ct);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — second confirm-sent on an already-PayerSent settlement
        var response = await scenario.Client2.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> GenerateAndGetSettlementIdAsync(SettlementScenario scenario, CancellationToken ct)
    {
        var generateUrl = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements";
        var response = await Client.PostAsync(generateUrl, null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
    }
}

// ---------------------------------------------------------------------------
// POST /settlements/{settlementId}/confirm-received
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ConfirmReceivedTests : IntegrationTestBase
{
    public ConfirmReceivedTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ConfirmReceived_ReceiverFamily_Returns200AndStatusIsCompleted()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateAndAdvanceToPayerSentAsync(scenario, ct);

        // Caller's family is the receiver — use the primary Client
        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-received";

        // Act
        var response = await Client.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Completed");
        doc.RootElement.GetProperty("completedAt").ValueKind
            .Should().NotBe(System.Text.Json.JsonValueKind.Null,
                "completedAt must be set when Completed");
        doc.RootElement.GetProperty("approvalSteps").GetArrayLength().Should().Be(2,
            "PayerSent step + ReceiverConfirmed step");
    }

    [Fact]
    public async Task ConfirmReceived_PayerFamilyTriesToConfirmReceived_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateAndAdvanceToPayerSentAsync(scenario, ct);

        // Family2 is the payer — they should NOT be able to confirm received
        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-received";

        // Act
        var response = await scenario.Client2.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConfirmReceived_StillProposed_Returns422()
    {
        // Arrange — settlement is in Proposed state (not yet PayerSent)
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);
        var settlementId = await GenerateSettlementIdAsync(scenario, ct);

        var url = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-received";

        // Act — skip confirm-sent and attempt confirm-received on Proposed settlement
        var response = await Client.PostAsync(url, null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> GenerateSettlementIdAsync(SettlementScenario scenario, CancellationToken ct)
    {
        var generateUrl = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements";
        var response = await Client.PostAsync(generateUrl, null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private async Task<Guid> GenerateAndAdvanceToPayerSentAsync(SettlementScenario scenario, CancellationToken ct)
    {
        var settlementId = await GenerateSettlementIdAsync(scenario, ct);

        var confirmSentUrl = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements/{settlementId}/confirm-sent";
        var sentResponse = await scenario.Client2.PostAsync(confirmSentUrl, null, ct);
        sentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return settlementId;
    }
}

// ---------------------------------------------------------------------------
// Full settlement flow: generate → confirm-sent → confirm-received
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class FullSettlementFlowTests : IntegrationTestBase
{
    public FullSettlementFlowTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FullFlow_GenerateConfirmSentConfirmReceived_SettlementCompletedAndActivitySettled()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var scenario = await SettlementScenarioHelper.SetupAsync(this, ct);

        var baseUrl = $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}/settlements";

        // Act 1 — Generate
        var generateResponse = await Client.PostAsync(baseUrl, null, ct);
        generateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var generateBody = await generateResponse.Content.ReadAsStringAsync(ct);
        using var generateDoc = System.Text.Json.JsonDocument.Parse(generateBody);
        var settlementId = generateDoc.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        generateDoc.RootElement.EnumerateArray().First()
            .GetProperty("status").GetString().Should().Be("Proposed");

        // Act 2 — Confirm sent (payer = family2)
        var confirmSentUrl = $"{baseUrl}/{settlementId}/confirm-sent";
        var sentResponse = await scenario.Client2.PostAsync(confirmSentUrl, null, ct);
        sentResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sentBody = await sentResponse.Content.ReadAsStringAsync(ct);
        using var sentDoc = System.Text.Json.JsonDocument.Parse(sentBody);
        sentDoc.RootElement.GetProperty("status").GetString().Should().Be("PayerSent");

        // Act 3 — Confirm received (receiver = caller's family)
        var confirmReceivedUrl = $"{baseUrl}/{settlementId}/confirm-received";
        var receivedResponse = await Client.PostAsync(confirmReceivedUrl, null, ct);
        receivedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var receivedBody = await receivedResponse.Content.ReadAsStringAsync(ct);
        using var receivedDoc = System.Text.Json.JsonDocument.Parse(receivedBody);
        receivedDoc.RootElement.GetProperty("status").GetString().Should().Be("Completed");
        receivedDoc.RootElement.GetProperty("completedAt").ValueKind
            .Should().NotBe(System.Text.Json.JsonValueKind.Null);

        // Assert — activity must be transitioned to Settled (all settlements completed)
        var activityResponse = await Client.GetAsync(
            $"/groups/{scenario.GroupId}/activities/{scenario.ActivityId}", ct);
        activityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var activityBody = await activityResponse.Content.ReadAsStringAsync(ct);
        using var activityDoc = System.Text.Json.JsonDocument.Parse(activityBody);
        activityDoc.RootElement.GetProperty("status").GetString().Should().Be("Settled",
            "all settlements completed → activity transitions to Settled");

        // Assert — GET /settlements returns the completed settlement
        var listResponse = await Client.GetAsync(baseUrl, ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        using var listDoc = System.Text.Json.JsonDocument.Parse(listBody);
        var listed = listDoc.RootElement.EnumerateArray().ToList();
        listed.Should().HaveCount(1);
        listed[0].GetProperty("id").GetGuid().Should().Be(settlementId);
        listed[0].GetProperty("status").GetString().Should().Be("Completed");

        // Assert — GET /settlements/{id} detail includes both approval steps
        var detailResponse = await Client.GetAsync($"{baseUrl}/{settlementId}", ct);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailBody = await detailResponse.Content.ReadAsStringAsync(ct);
        using var detailDoc = System.Text.Json.JsonDocument.Parse(detailBody);
        detailDoc.RootElement.GetProperty("approvalSteps").GetArrayLength().Should().Be(2);
    }
}

// ---------------------------------------------------------------------------
// Shared scenario setup helper — file-scoped static class
// ---------------------------------------------------------------------------

static class SettlementScenarioHelper
{
    // Must match IntegrationTestBase.TestSigningKey — duplicated here because
    // the protected const is not accessible from a file-scoped static class.
    private const string SigningKey = "integration-test-signing-key-xxxxxxxxxxxxxxxx";
    /// <summary>
    /// Seeds a full scenario: two families in a group, one activity, one expense
    /// paid by the caller's family (creating an imbalance), then closes the activity.
    /// Returns the scenario record including an authenticated HttpClient for family2.
    /// </summary>
    public static async Task<SettlementScenario> SetupAsync(
        IntegrationTestBase test,
        CancellationToken ct)
    {
        // 1. Seed family2 + member2 (no User yet)
        var (family2Id, member2Id) = await test.SeedExtraFamilyAsync("Second Family", "Second Member");

        // 2. Seed a group with both families
        var (_, groupId) = await SeedGroupWithBothFamiliesAsync(test, ct, family2Id);

        // 3. Seed a User for family2 and link it to member2
        var user2Id = Guid.NewGuid();
        await using (var cmd = test.Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @ext, 'Google', @email, 'Family2 User', false, now())
                """;
            cmd.Parameters.AddWithValue("id", user2Id);
            cmd.Parameters.AddWithValue("ext", "google-family2-" + user2Id.ToString("N"));
            cmd.Parameters.AddWithValue("email", "family2@integration.test");
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = test.Connection.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE family_members
                SET user_id = @uid, email = 'family2@integration.test'
                WHERE id = @mid
                """;
            cmd.Parameters.AddWithValue("uid", user2Id);
            cmd.Parameters.AddWithValue("mid", member2Id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 4. Create an authenticated client for family2
        var token2 = JwtHelper.Mint(user2Id, "family2@integration.test", "Family2 User",
            false, SigningKey);
        var client2 = test.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client2.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token2);

        // 5. Create an activity via API (auto-seeds participants from both families)
        var createActivityResponse = await test.Client.PostAsync(
            $"/groups/{groupId}/activities",
            System.Net.Http.Json.JsonContent.Create(
                new { name = "Settlement Test Activity", description = (string?)null }),
            ct);
        createActivityResponse.EnsureSuccessStatusCode();

        var activityBody = await createActivityResponse.Content.ReadAsStringAsync(ct);
        using var activityDoc = System.Text.Json.JsonDocument.Parse(activityBody);
        var activityId = activityDoc.RootElement.GetProperty("id").GetGuid();

        // 6. Create an expense paid by the caller (CallerFamilyId) — totalAmount €100
        //    Both families participate, so caller is owed ~€50 by family2
        var expenseResponse = await test.Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses",
            System.Net.Http.Json.JsonContent.Create(new
            {
                title = "Settlement Test Expense",
                description = (string?)null,
                totalAmount = 100m,
                currency = "EUR",
                expenseDate = System.DateOnly.FromDateTime(System.DateTime.UtcNow.Date),
                categoryId = (Guid?)null,
            }),
            ct);
        expenseResponse.EnsureSuccessStatusCode();

        // 7. Close the activity
        var closeResponse = await test.Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/close", null, ct);
        closeResponse.EnsureSuccessStatusCode();

        return new SettlementScenario(groupId, activityId, family2Id, member2Id, user2Id, client2);
    }

    /// <summary>
    /// Seeds a group with both the caller's family and a given second family.
    /// Returns (family2Id, groupId). If family2Id is Guid.Empty a fresh one is seeded.
    /// </summary>
    public static async Task<(Guid family2Id, Guid groupId)> SeedGroupWithBothFamiliesAsync(
        IntegrationTestBase test,
        CancellationToken ct,
        Guid family2Id = default)
    {
        if (family2Id == Guid.Empty)
            (family2Id, _) = await test.SeedExtraFamilyAsync("Second Family", "Second Member");

        var groupId = Guid.NewGuid();
        var inviteCode = Guid.NewGuid().ToString("N")[..8];

        await using (var cmd = test.Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO groups (id, name, invite_code, created_by_user_id, created_at, updated_at)
                VALUES (@id, @name, @code, @creator, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", groupId);
            cmd.Parameters.AddWithValue("name", "Settlement Test Group");
            cmd.Parameters.AddWithValue("code", inviteCode);
            cmd.Parameters.AddWithValue("creator", test.CallerId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Add caller's family as Admin
        await using (var cmd = test.Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO group_families (id, group_id, family_id, role, joined_at)
                VALUES (@id, @gid, @fid, 'Admin', now())
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("gid", groupId);
            cmd.Parameters.AddWithValue("fid", test.CallerFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Add second family as Member
        await using (var cmd = test.Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO group_families (id, group_id, family_id, role, joined_at)
                VALUES (@id, @gid, @fid, 'Member', now())
                """;
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("gid", groupId);
            cmd.Parameters.AddWithValue("fid", family2Id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return (family2Id, groupId);
    }
}
