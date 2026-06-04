using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests;

// ---------------------------------------------------------------------------
// Collection fixture wiring — one Postgres container shared by all tests
// in this collection. The container is started once and migrations are applied
// once; individual tests isolate themselves via per-test transaction rollback.
// ---------------------------------------------------------------------------

[CollectionDefinition(nameof(IntegrationCollection))]
public sealed class IntegrationCollection : ICollectionFixture<PostgresContainerFixture> { }

// ---------------------------------------------------------------------------
// Health endpoint — anonymous, no DB involvement
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class HealthProofTests : IntegrationTestBase
{
    public HealthProofTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Health_endpoint_returns_200_ok()
    {
        // Arrange — create an anonymous (no Bearer header) client from the same factory.
        using var anonymousClient = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            });

        // Act
        var ct = TestContext.Current.CancellationToken;
        var response = await anonymousClient.GetAsync("/health", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }
}

// ---------------------------------------------------------------------------
// WhoAmI endpoint — authenticated, requires a valid JWT + seeded User row
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class AuthProofTests : IntegrationTestBase
{
    public AuthProofTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task WhoAmI_with_valid_jwt_returns_200_and_non_empty_email()
    {
        // Act — Client already has Authorization: Bearer set by IntegrationTestBase.
        var ct = TestContext.Current.CancellationToken;
        var response = await Client.GetAsync("/whoami", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("email").GetString()
            .Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WhoAmI_without_jwt_returns_401()
    {
        // Arrange — anonymous client, no Authorization header.
        using var anonymousClient = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            });

        // Act
        var response = await anonymousClient.GetAsync("/whoami", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
