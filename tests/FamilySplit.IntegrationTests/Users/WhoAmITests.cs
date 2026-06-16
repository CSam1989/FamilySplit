using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests.Users;

/// <summary>
/// Testcontainers coverage for <c>GET /whoami</c> (the Users slice's query handler — pure data
/// access, ADR-001). The legacy inline endpoint's projection shape is the read-side wire-format
/// lock, so this asserts every field the client depends on against real PostgreSQL, plus the
/// unknown-user → 404 path. Happy-path 200 + 401-without-jwt are also covered by
/// <c>AuthProofTests</c> in <c>ProofTests.cs</c>.
/// </summary>
[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class WhoAmITests : IntegrationTestBase
{
    public WhoAmITests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task WhoAmI_SeededUser_ReturnsFullProjection()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/whoami", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.GetProperty("id").GetGuid().Should().Be(CallerId);
        root.GetProperty("email").GetString().Should().Be("testuser@integration.test");
        root.GetProperty("displayName").GetString().Should().Be("Integration Test User");
        root.GetProperty("provider").GetString().Should().Be("Google");
        root.GetProperty("isGlobalAdmin").GetBoolean().Should().BeFalse();
        root.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("createdAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task WhoAmI_UnknownUser_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        // A valid JWT for a user id that has no row in the users table.
        using var client = CreateClientForUser(Guid.NewGuid());

        var response = await client.GetAsync("/whoami", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private HttpClient CreateClientForUser(Guid userId)
    {
        var token = JwtHelper.Mint(
            userId: userId,
            email: $"whoami-{userId:N}@integration.test",
            displayName: "WhoAmI User",
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
