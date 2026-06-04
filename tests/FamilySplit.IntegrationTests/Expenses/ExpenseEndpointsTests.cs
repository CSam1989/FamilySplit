using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Expenses;

// ── Single-participant expense (calculatedAmount == totalAmount) ────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class SingleParticipantExpenseTests : IntegrationTestBase
{
    public SingleParticipantExpenseTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExpense_SingleParticipant_CalculatedAmountEqualsTotalAmount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Set caller DOB so they are Volwassene (weight 1.00) on the expense date
        await SetMemberDobAsync(CallerMemberId, new DateOnly(2000, 1, 1), ct);

        // Create activity — auto-seeds CallerMember as sole participant
        var activityId = await CreateActivityAsync(groupId, "Solo Activity", ct);

        var payload = JsonContent.Create(new
        {
            title = "Dinner",
            description = (string?)null,
            totalAmount = 100.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        // Act
        var createResponse = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);

        // Assert — 201 Created
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var expenseId = createDoc.RootElement.GetProperty("id").GetGuid();

        // GET detail to verify participant's calculatedAmount
        var detailResponse = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailBody = await detailResponse.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);

        var participants = detailDoc.RootElement.GetProperty("participants");
        participants.GetArrayLength().Should().Be(1);

        var participant = participants.EnumerateArray().First();
        participant.GetProperty("familyMemberId").GetGuid().Should().Be(CallerMemberId);
        participant.GetProperty("weightSnapshot").GetDecimal().Should().Be(1.00m);
        participant.GetProperty("calculatedAmount").GetDecimal().Should().Be(100.00m);
        participant.GetProperty("isExcluded").GetBoolean().Should().BeFalse();
    }

    private async Task SetMemberDobAsync(Guid memberId, DateOnly dob, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE family_members SET date_of_birth = @dob WHERE id = @id";
        cmd.Parameters.AddWithValue("dob", dob.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("id", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Two-participant weight-based split ─────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class TwoParticipantSplitTests : IntegrationTestBase
{
    public TwoParticipantSplitTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExpense_TwoParticipants_SplitByWeight()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Participant A = Volwassene (weight 1.00): born 2000-01-01, expense date 2026-01-01 → age 26
        await SetMemberDobAsync(CallerMemberId, new DateOnly(2000, 1, 1), ct);

        // Participant B = MiddelbaarOnderwijs (weight 0.75): born 2011-01-01, expense date 2026-01-01 → age 15
        var (secondFamilyId, secondMemberId) = await SeedExtraFamilyAsync("Second Split Family", "Teen Member");
        await SetMemberDobAsync(secondMemberId, new DateOnly(2011, 1, 1), ct);

        // Add second family to the group
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO group_families (id, group_id, family_id, role, joined_at) VALUES (@id, @groupId, @familyId, 'Member', now())";
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("groupId", groupId);
            cmd.Parameters.AddWithValue("familyId", secondFamilyId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Create activity — both members are auto-seeded as participants
        var activityId = await CreateActivityAsync(groupId, "Two-Person Trip", ct);

        // Verify both members are seeded
        var detailBefore = await Client.GetAsync($"/groups/{groupId}/activities/{activityId}", ct);
        var detailBeforeBody = await detailBefore.Content.ReadAsStringAsync(ct);
        using var detailBeforeDoc = JsonDocument.Parse(detailBeforeBody);
        detailBeforeDoc.RootElement.GetProperty("participants").GetArrayLength().Should().Be(2);

        // Create expense: €100 on 2026-01-01
        // A weight=1.00, B weight=0.75, total weight=1.75
        // A share = 100 * 1.00 / 1.75 = 57.142857... → rounded = 57.14
        // B share = 100 * 0.75 / 1.75 = 42.857142... → rounded = 42.86
        // Rounding remainder goes to A (heaviest) → A = 57.14, B = 42.86 (sum = 100.00 ✓)
        var payload = JsonContent.Create(new
        {
            title = "Trip Expenses",
            description = (string?)null,
            totalAmount = 100.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        var createResponse = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var expenseId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Act — GET expense detail
        var detailResponse = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);

        // Assert
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailBody = await detailResponse.Content.ReadAsStringAsync(ct);
        using var detailDoc = JsonDocument.Parse(detailBody);

        var participants = detailDoc.RootElement.GetProperty("participants")
            .EnumerateArray().ToList();
        participants.Should().HaveCount(2);

        var participantA = participants.First(p =>
            p.GetProperty("familyMemberId").GetGuid() == CallerMemberId);
        var participantB = participants.First(p =>
            p.GetProperty("familyMemberId").GetGuid() == secondMemberId);

        participantA.GetProperty("weightSnapshot").GetDecimal().Should().Be(1.00m);
        participantB.GetProperty("weightSnapshot").GetDecimal().Should().Be(0.75m);

        // Verify calculated amounts sum to 100.00 and match expected shares
        var amountA = participantA.GetProperty("calculatedAmount").GetDecimal();
        var amountB = participantB.GetProperty("calculatedAmount").GetDecimal();

        (amountA + amountB).Should().Be(100.00m, "shares must sum to total");
        amountA.Should().Be(57.14m, "Volwassene (weight 1.00) gets larger share");
        amountB.Should().Be(42.86m, "Middelbaar (weight 0.75) gets smaller share");
    }

    private async Task SetMemberDobAsync(Guid memberId, DateOnly dob, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE family_members SET date_of_birth = @dob WHERE id = @id";
        cmd.Parameters.AddWithValue("dob", dob.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("id", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Validation and authorization ───────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ExpenseValidationTests : IntegrationTestBase
{
    public ExpenseValidationTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExpense_MissingTitle_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Validation Activity", ct);

        var payload = JsonContent.Create(new
        {
            title = "",
            totalAmount = 50.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        // Act
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Title")
            .EnumerateArray().First().GetString()
            .Should().Be("Title is required.");
    }

    [Fact]
    public async Task CreateExpense_NonMember_Returns403()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Non-member Activity", ct);

        // Seed an outsider user+family not in the group
        var outsiderId = await SeedOutsiderUserAsync(ct);
        using var outsiderClient = CreateClientForUser(outsiderId);

        var payload = JsonContent.Create(new
        {
            title = "Unauthorized Expense",
            totalAmount = 10.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        // Act
        var response = await outsiderClient.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);

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
            cmd.Parameters.AddWithValue("name", "Outsider Expense Family");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-exp-out-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "expoutsider@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Expense Outsider");
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
            cmd.Parameters.AddWithValue("email", "expoutsider@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Expense Outsider");
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return userId;
    }

    private HttpClient CreateClientForUser(Guid userId)
    {
        var token = JwtHelper.Mint(
            userId: userId,
            email: "expoutsider@integration.test",
            displayName: "Expense Outsider",
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

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Delete expense ─────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class DeleteExpenseTests : IntegrationTestBase
{
    public DeleteExpenseTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DeleteExpense_OpenActivity_Returns204()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Delete Test Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "To Delete", ct);

        // Act
        var response = await Client.DeleteAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify gone from list
        var listResponse = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.EnumerateArray()
            .Any(e => e.GetProperty("id").GetGuid() == expenseId)
            .Should().BeFalse("deleted expense must not appear in list");
    }

    [Fact]
    public async Task DeleteExpense_SettledActivity_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Settled Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Expense On Settled", ct);

        // Settle the activity by: close it, generate settlements (will be zero-balance
        // since single family — but we need status=Settled). Instead force status in DB.
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE activities SET status = 'Settled' WHERE id = @id";
            cmd.Parameters.AddWithValue("id", activityId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Act
        var response = await Client.DeleteAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<Guid> CreateExpenseAsync(Guid groupId, Guid activityId, string title, CancellationToken ct)
    {
        var payload = JsonContent.Create(new
        {
            title,
            description = (string?)null,
            totalAmount = 25.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Update expense ─────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateExpenseTests : IntegrationTestBase
{
    public UpdateExpenseTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateExpense_ChangeAmount_Returns200WithUpdatedAmount()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Update Test Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Original Expense", 50.00m, "2026-01-01", ct);

        var updatePayload = JsonContent.Create(new
        {
            title = "Updated Expense",
            description = (string?)null,
            totalAmount = 75.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        // Act
        var response = await Client.PutAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", updatePayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("totalAmount").GetDecimal().Should().Be(75.00m);
        doc.RootElement.GetProperty("title").GetString().Should().Be("Updated Expense");

        // Verify participant calculatedAmount is recalculated to match new total
        var participants = doc.RootElement.GetProperty("participants").EnumerateArray().ToList();
        participants.Sum(p => p.GetProperty("calculatedAmount").GetDecimal())
            .Should().Be(75.00m, "recalculated shares must sum to new total");
    }

    [Fact]
    public async Task UpdateExpense_ChangeDate_ReSnapshotsWeights()
    {
        // Arrange — member turns 18 between the two dates, changing their weight tier
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Born 2008-07-01: on 2026-01-01 they are 17 (MiddelbaarOnderwijs, weight 0.75)
        //                  on 2026-09-01 they are 18 (Volwassene, weight 1.00)
        await SetMemberDobAsync(CallerMemberId, new DateOnly(2008, 7, 1), ct);

        var activityId = await CreateActivityAsync(groupId, "Re-snapshot Activity", ct);

        // Create expense at date where member is still Middelbaar (weight 0.75)
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Date Change Expense", 100.00m, "2026-01-01", ct);

        // Verify initial snapshot
        var beforeDetail = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);
        var beforeBody = await beforeDetail.Content.ReadAsStringAsync(ct);
        using var beforeDoc = JsonDocument.Parse(beforeBody);
        var beforeSnapshot = beforeDoc.RootElement.GetProperty("participants")
            .EnumerateArray().First(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .GetProperty("weightSnapshot").GetDecimal();
        beforeSnapshot.Should().Be(0.75m, "member is 17 on 2026-01-01");

        // Update expense date to after their 18th birthday
        var updatePayload = JsonContent.Create(new
        {
            title = "Date Change Expense",
            description = (string?)null,
            totalAmount = 100.00m,
            currency = "EUR",
            expenseDate = "2026-09-01",
            categoryId = (Guid?)null,
        });

        // Act
        var updateResponse = await Client.PutAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", updatePayload, ct);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterBody = await updateResponse.Content.ReadAsStringAsync(ct);
        using var afterDoc = JsonDocument.Parse(afterBody);
        var afterSnapshot = afterDoc.RootElement.GetProperty("participants")
            .EnumerateArray().First(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .GetProperty("weightSnapshot").GetDecimal();
        afterSnapshot.Should().Be(1.00m, "member turns 18 before 2026-09-01, weight re-snapshotted to Volwassene");
    }

    private async Task SetMemberDobAsync(Guid memberId, DateOnly dob, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE family_members SET date_of_birth = @dob WHERE id = @id";
        cmd.Parameters.AddWithValue("dob", dob.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("id", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Guid> CreateExpenseAsync(
        Guid groupId, Guid activityId, string title, decimal amount, string date, CancellationToken ct)
    {
        var payload = JsonContent.Create(new
        {
            title,
            description = (string?)null,
            totalAmount = amount,
            currency = "EUR",
            expenseDate = date,
            categoryId = (Guid?)null,
        });
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Delete locked expense ──────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class DeleteLockedExpenseTests : IntegrationTestBase
{
    public DeleteLockedExpenseTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task DeleteExpense_LockedExpense_Returns422()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Locked Expense Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Locked Expense", ct);

        // Force the expense into Locked status directly in the DB
        await using (var cmd = Connection.CreateCommand())
        {
            cmd.CommandText = "UPDATE expenses SET status = 'Locked' WHERE id = @id";
            cmd.Parameters.AddWithValue("id", expenseId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Act
        var response = await Client.DeleteAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Status")
            .EnumerateArray().First().GetString()
            .Should().Be("This expense is locked and cannot be deleted.");
    }

    private async Task<Guid> CreateExpenseAsync(Guid groupId, Guid activityId, string title, CancellationToken ct)
    {
        var payload = JsonContent.Create(new
        {
            title,
            description = (string?)null,
            totalAmount = 30.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Update expense — title-only change does not re-snapshot weights ────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class UpdateExpenseTitleOnlyTests : IntegrationTestBase
{
    public UpdateExpenseTitleOnlyTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UpdateExpense_TitleOnlyChange_DoesNotReSnapshotWeights()
    {
        // Arrange — fix a specific DOB so the weight is deterministic
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);

        // Born 2000-01-01: on 2026-01-01 they are 26 → Volwassene, weight 1.00
        await SetMemberDobAsync(CallerMemberId, new DateOnly(2000, 1, 1), ct);

        var activityId = await CreateActivityAsync(groupId, "Title-Only Update Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Original Title", 50.00m, "2026-01-01", ct);

        // Note the original weightSnapshot from the detail response
        var beforeDetail = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);
        beforeDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var beforeBody = await beforeDetail.Content.ReadAsStringAsync(ct);
        using var beforeDoc = JsonDocument.Parse(beforeBody);
        var originalSnapshot = beforeDoc.RootElement.GetProperty("participants")
            .EnumerateArray()
            .First(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .GetProperty("weightSnapshot").GetDecimal();
        originalSnapshot.Should().Be(1.00m, "Volwassene at age 26");

        // PUT — only the title changes; amount and date are identical
        var updatePayload = JsonContent.Create(new
        {
            title = "Updated Title",
            description = (string?)null,
            totalAmount = 50.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });

        // Act
        var response = await Client.PutAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", updatePayload, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterBody = await response.Content.ReadAsStringAsync(ct);
        using var afterDoc = JsonDocument.Parse(afterBody);

        afterDoc.RootElement.GetProperty("title").GetString().Should().Be("Updated Title");

        var afterSnapshot = afterDoc.RootElement.GetProperty("participants")
            .EnumerateArray()
            .First(p => p.GetProperty("familyMemberId").GetGuid() == CallerMemberId)
            .GetProperty("weightSnapshot").GetDecimal();

        afterSnapshot.Should().Be(originalSnapshot,
            "weight snapshots must be unchanged when only the title is updated");
    }

    private async Task SetMemberDobAsync(Guid memberId, DateOnly dob, CancellationToken ct)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "UPDATE family_members SET date_of_birth = @dob WHERE id = @id";
        cmd.Parameters.AddWithValue("dob", dob.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("id", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<Guid> CreateExpenseAsync(
        Guid groupId, Guid activityId, string title, decimal amount, string date, CancellationToken ct)
    {
        var payload = JsonContent.Create(new
        {
            title,
            description = (string?)null,
            totalAmount = amount,
            currency = "EUR",
            expenseDate = date,
            categoryId = (Guid?)null,
        });
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── Audit log rows ─────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ExpenseAuditLogTests : IntegrationTestBase
{
    public ExpenseAuditLogTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExpense_WritesAuditLogRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Audit Create Activity", ct);

        // Act
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Audit Create Expense", ct);

        // Assert — one Created row must exist in audit_log for this expense
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM audit_log
            WHERE entity_type = 'Expense'
              AND action = 'Created'
              AND entity_id = @expenseId
            """;
        cmd.Parameters.AddWithValue("expenseId", expenseId);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        count.Should().Be(1, "CreateAsync must write exactly one audit log row");
    }

    [Fact]
    public async Task UpdateExpense_WritesAuditLogRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Audit Update Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Audit Update Expense", ct);

        var updatePayload = JsonContent.Create(new
        {
            title = "Audit Update Expense — Edited",
            description = (string?)null,
            totalAmount = 99.00m,
            currency = "EUR",
            expenseDate = "2026-06-01",
            categoryId = (Guid?)null,
        });

        // Act
        var response = await Client.PutAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", updatePayload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — one Updated row must exist in audit_log for this expense
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM audit_log
            WHERE entity_type = 'Expense'
              AND action = 'Updated'
              AND entity_id = @expenseId
            """;
        cmd.Parameters.AddWithValue("expenseId", expenseId);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        count.Should().Be(1, "UpdateAsync must write exactly one audit log row");
    }

    [Fact]
    public async Task DeleteExpense_WritesAuditLogRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "Audit Delete Activity", ct);
        var expenseId = await CreateExpenseAsync(groupId, activityId, "Audit Delete Expense", ct);

        // Act
        var response = await Client.DeleteAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses/{expenseId}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — one Deleted row must exist in audit_log for this expense
        // Note: the expense row itself is hard-deleted, but the audit_log row persists.
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM audit_log
            WHERE entity_type = 'Expense'
              AND action = 'Deleted'
              AND entity_id = @expenseId
            """;
        cmd.Parameters.AddWithValue("expenseId", expenseId);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        count.Should().Be(1, "DeleteAsync must write exactly one audit log row");
    }

    private async Task<Guid> CreateExpenseAsync(Guid groupId, Guid activityId, string title, CancellationToken ct)
    {
        var payload = JsonContent.Create(new
        {
            title,
            description = (string?)null,
            totalAmount = 45.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });
        var response = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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

// ── List expenses ──────────────────────────────────────────────────────────────

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class ListExpensesTests : IntegrationTestBase
{
    public ListExpensesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListExpenses_Returns200_WithCreatedExpense()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var groupId = await SeedGroupAsync(ct);
        var activityId = await CreateActivityAsync(groupId, "List Test Activity", ct);

        var createPayload = JsonContent.Create(new
        {
            title = "Listed Expense",
            description = (string?)null,
            totalAmount = 40.00m,
            currency = "EUR",
            expenseDate = "2026-01-01",
            categoryId = (Guid?)null,
        });
        var createResponse = await Client.PostAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", createPayload, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        using var createDoc = JsonDocument.Parse(createBody);
        var expenseId = createDoc.RootElement.GetProperty("id").GetGuid();

        // Act
        var listResponse = await Client.GetAsync(
            $"/groups/{groupId}/activities/{activityId}/expenses", ct);

        // Assert
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadAsStringAsync(ct);
        using var listDoc = JsonDocument.Parse(listBody);
        listDoc.RootElement.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        listDoc.RootElement.EnumerateArray()
            .Any(e => e.GetProperty("id").GetGuid() == expenseId)
            .Should().BeTrue("created expense must appear in list");
    }

    private async Task<Guid> CreateActivityAsync(Guid groupId, string name, CancellationToken ct)
    {
        var payload = JsonContent.Create(new { name });
        var response = await Client.PostAsync($"/groups/{groupId}/activities", payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
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
