using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Infrastructure;
using FamilySplit.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.IntegrationTests.Dashboard;

/// <summary>
/// Testcontainers coverage for <c>GET /dashboard/stats</c> (the Dashboard slice's query handler —
/// pure data access, ADR-001). Ported from the former InMemory <c>GetStatsQueryHandlerTests</c> so the
/// dashboard aggregation (GroupBy/Sum, cross-join "paid", settled exclusion, sub-activity roll-up,
/// pending-settlement join) is verified against real PostgreSQL rather than the InMemory provider.
/// Data is seeded through the shared test transaction via EF, then read back through the HTTP API.
/// </summary>
[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class DashboardStatsTests : IntegrationTestBase
{
    public DashboardStatsTests(PostgresContainerFixture fixture) : base(fixture) { }

    private static readonly DateOnly Date = new(2026, 1, 1);
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    // ── Tests ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_NoFamilyMembership_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClientForUser(Guid.NewGuid());

        var response = await client.GetAsync("/dashboard/stats", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_InactiveMembership_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;
        var userId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = userId,
                Provider = Provider.Google,
                ExternalId = "google-inactive-" + userId.ToString("N"),
                Email = $"inactive-{userId:N}@integration.test",
                DisplayName = "Inactive",
                CreatedAt = Now,
            });
            db.FamilyMembers.Add(new FamilyMember
            {
                Id = Guid.NewGuid(),
                FamilyId = CallerFamilyId,
                UserId = userId,
                DisplayName = "Inactive",
                IsActive = false,
            });
        }, ct);

        using var client = CreateClientForUser(userId);
        var response = await client.GetAsync("/dashboard/stats", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_NoGroups_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var stats = await GetStatsAsync(ct);

        stats.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GetStats_GroupWithNoActivities_ReturnsZeroStats()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db => AddGroup(db, groupId, "Group1"), ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("groupName").GetString().Should().Be("Group1");
        stat.GetProperty("totalActivities").GetInt32().Should().Be(0);
        stat.GetProperty("totalGroupSpend").GetDecimal().Should().Be(0);
        stat.GetProperty("currency").GetString().Should().Be("EUR");
        stat.GetProperty("netBalance").GetDecimal().Should().Be(0);
        stat.GetProperty("pendingSettlements").GetInt32().Should().Be(0);
        stat.GetProperty("latestActivityName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetStats_WithActivities_ReturnsCorrectCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            AddActivity(db, groupId, "A1", ActivityStatus.Open, Now.AddDays(-2));
            AddActivity(db, groupId, "A2", ActivityStatus.Closed, Now.AddDays(-1));
            AddActivity(db, groupId, "A3", ActivityStatus.Settled, Now);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("totalActivities").GetInt32().Should().Be(3);
        stat.GetProperty("openActivities").GetInt32().Should().Be(1);
        stat.GetProperty("closedActivities").GetInt32().Should().Be(1);
        stat.GetProperty("settledActivities").GetInt32().Should().Be(1);
        stat.GetProperty("latestActivityName").GetString().Should().Be("A3");
        stat.GetProperty("latestActivityStatus").GetString().Should().Be("Settled");
    }

    [Fact]
    public async Task GetStats_WithExpenses_ReturnsTotalSpendAndShare()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var actId = AddActivity(db, groupId, "Trip", ActivityStatus.Open, Now);
            var expId = AddExpense(db, actId, 100m, "USD");
            AddParticipant(db, expId, 50m);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("totalGroupSpend").GetDecimal().Should().Be(100m);
        stat.GetProperty("myFamilyShare").GetDecimal().Should().Be(50m);
        stat.GetProperty("currency").GetString().Should().Be("USD");
        stat.GetProperty("activeGroupSpend").GetDecimal().Should().Be(100m);
        stat.GetProperty("activeFamilyShare").GetDecimal().Should().Be(50m);
    }

    [Fact]
    public async Task GetStats_NetBalance_PaidMinusOwed()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var actId = AddActivity(db, groupId, "Trip", ActivityStatus.Open, Now);
            var expId = AddExpense(db, actId, 200m);          // paid by caller
            AddParticipant(db, expId, 80m);                   // caller owes 80
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("netBalance").GetDecimal().Should().Be(120m); // 200 paid − 80 owed
    }

    [Fact]
    public async Task GetStats_SettledActivities_ExcludedFromActiveSpendAndBalance()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var actId = AddActivity(db, groupId, "Old", ActivityStatus.Settled, Now);
            var expId = AddExpense(db, actId, 500m);
            AddParticipant(db, expId, 250m);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("totalGroupSpend").GetDecimal().Should().Be(500m); // historical includes settled
        stat.GetProperty("activeGroupSpend").GetDecimal().Should().Be(0m);  // settled excluded
        stat.GetProperty("netBalance").GetDecimal().Should().Be(0m);        // settled excluded
    }

    [Fact]
    public async Task GetStats_PendingSettlements_CountsCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        var (otherFamilyId, _) = await SeedExtraFamilyAsync("Dashboard Other Family", "Other Member");
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var actId = AddActivity(db, groupId, "Trip", ActivityStatus.Open, Now);
            // Proposed where caller is payer → pending (confirm-sent)
            AddSettlement(db, actId, CallerFamilyId, otherFamilyId, 50m, SettlementStatus.Proposed);
            // PayerSent where caller is receiver → pending (confirm-received)
            AddSettlement(db, actId, otherFamilyId, CallerFamilyId, 30m, SettlementStatus.PayerSent);
            // Completed → not pending. On a separate activity to respect the unique
            // (activity_id, payer_family_id, receiver_family_id) index.
            var actId2 = AddActivity(db, groupId, "Trip 2", ActivityStatus.Open, Now);
            AddSettlement(db, actId2, CallerFamilyId, otherFamilyId, 10m, SettlementStatus.Completed);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("pendingSettlements").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetStats_ExcludedParticipant_NotCountedInShare()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var actId = AddActivity(db, groupId, "Trip", ActivityStatus.Open, Now);
            var expId = AddExpense(db, actId, 100m);
            AddParticipant(db, expId, 50m, excluded: true);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("myFamilyShare").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task GetStats_SubActivities_ExcludedFromTopLevelCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var groupId = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, groupId, "Group1");
            var parentId = AddActivity(db, groupId, "Parent", ActivityStatus.Open, Now);
            AddActivity(db, groupId, "Child", ActivityStatus.Open, Now, parentId);
        }, ct);

        var stat = StatFor(await GetStatsAsync(ct), groupId);

        stat.GetProperty("totalActivities").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetStats_MultipleGroups_ReturnsStatsForEach()
    {
        var ct = TestContext.Current.CancellationToken;
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        await SeedAsync(db =>
        {
            AddGroup(db, g1, "Alpha");
            AddGroup(db, g2, "Beta");
        }, ct);

        var stats = await GetStatsAsync(ct);

        new[] { StatFor(stats, g1), StatFor(stats, g2) }
            .Select(s => s.GetProperty("groupName").GetString())
            .Should().BeEquivalentTo("Alpha", "Beta");
    }

    // ── Seed helpers (EF, via the shared test transaction) ────────────────────────

    private async Task SeedAsync(Action<AppDbContext> seed, CancellationToken ct)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync(ct);
    }

    private void AddGroup(AppDbContext db, Guid groupId, string name)
    {
        db.Groups.Add(new Group
        {
            Id = groupId,
            Name = name,
            InviteCode = Guid.NewGuid().ToString("N")[..8],
            CreatedByUserId = CallerId,
            CreatedAt = Now,
            UpdatedAt = Now,
        });
        db.GroupFamilies.Add(new GroupFamily
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            FamilyId = CallerFamilyId,
            Role = MemberRole.Admin,
            JoinedAt = Now,
        });
    }

    private Guid AddActivity(AppDbContext db, Guid groupId, string name, ActivityStatus status, DateTimeOffset createdAt, Guid? parentId = null)
    {
        var id = Guid.NewGuid();
        db.Activities.Add(new Activity
        {
            Id = id,
            GroupId = groupId,
            Name = name,
            Status = status,
            ParentActivityId = parentId,
            CreatedByUserId = CallerId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        return id;
    }

    private Guid AddExpense(AppDbContext db, Guid activityId, decimal amount, string currency = "EUR")
    {
        var id = Guid.NewGuid();
        db.Expenses.Add(new Expense
        {
            Id = id,
            ActivityId = activityId,
            PaidByUserId = CallerId,
            Title = "Expense",
            TotalAmount = amount,
            Currency = currency,
            ExpenseDate = Date,
            Status = ExpenseStatus.Active,
            CreatedAt = Now,
            UpdatedAt = Now,
        });
        return id;
    }

    private void AddParticipant(AppDbContext db, Guid expenseId, decimal calculatedAmount, bool excluded = false)
    {
        db.ExpenseParticipants.Add(new ExpenseParticipant
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            FamilyMemberId = CallerMemberId,
            WeightSnapshot = 1m,
            CalculatedAmount = calculatedAmount,
            IsExcluded = excluded,
        });
    }

    private static void AddSettlement(AppDbContext db, Guid activityId, Guid payerFamilyId, Guid receiverFamilyId, decimal amount, SettlementStatus status)
    {
        db.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            ActivityId = activityId,
            PayerFamilyId = payerFamilyId,
            ReceiverFamilyId = receiverFamilyId,
            Amount = amount,
            Currency = "EUR",
            Status = status,
            ProposedAt = Now,
        });
    }

    // ── Read + assertion helpers ──────────────────────────────────────────────────

    private async Task<JsonElement> GetStatsAsync(CancellationToken ct)
    {
        var response = await Client.GetAsync("/dashboard/stats", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static JsonElement StatFor(JsonElement stats, Guid groupId) =>
        stats.EnumerateArray().First(e => e.GetProperty("groupId").GetGuid() == groupId);

    private HttpClient CreateClientForUser(Guid userId)
    {
        var token = JwtHelper.Mint(
            userId: userId,
            email: $"dashuser-{userId:N}@integration.test",
            displayName: "Dashboard User",
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
}
