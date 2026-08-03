using FamilySplit.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;

namespace FamilySplit.IntegrationTests;

// ---------------------------------------------------------------------------
// Phase 14 hardening — smoke tests for the seams the vertical-slice migration
// introduced across module boundaries. Each of these would have silently
// broken if a module's RegisterServices/MapEndpoints wiring were wrong in a
// way no other test happens to exercise:
//   - the "auth" named rate-limit policy is registered by AuthModule via a
//     *second* AddRateLimiter call composed with the host's own — if that
//     composition ever breaks, .RequireRateLimiting("auth") throws at
//     request time and every /auth/* route starts 500ing.
//   - the SignalR hub is mapped by NotificationsModule, not the host — if
//     that mapping is ever dropped, /hubs/notifications/negotiate 404s.
//   - the dev-only OpenAPI document is generated from whatever endpoints are
//     mapped when the app starts — since every domain route now comes from
//     a feature module's MapEndpoints, the document silently going empty
//     would mean the module array itself failed to wire up.
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class SeamSmokeTests : IntegrationTestBase
{
    public SeamSmokeTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AuthRefresh_WithoutCookie_ReturnsUnauthorized_NotServerError()
    {
        // Proves AuthModule's own AddRateLimiter call (registering the "auth"
        // named policy) composes with the host's AddRateLimiter call rather
        // than replacing it — if the policy were missing, RequireRateLimiting
        // would throw at request time and this would 500, not 401.
        using var anonymousClient = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            });

        var response = await anonymousClient.PostAsync(
            "/auth/refresh", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NotificationsHubNegotiate_WithoutAuth_ReturnsUnauthorized_NotNotFound()
    {
        // Proves NotificationsModule actually maps NotificationHub at
        // HubPaths.Notifications — if the hub weren't mapped this route
        // wouldn't exist at all and SignalR's negotiate endpoint would 404
        // instead of the [Authorize] attribute rejecting it with 401.
        using var anonymousClient = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            });

        var response = await anonymousClient.PostAsync(
            "/hubs/notifications/negotiate", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DevOpenApiDocument_ListsEndpointsFromMultipleFeatureModules()
    {
        // The OpenAPI document generator is only registered in Development
        // (Program.cs) — force that environment for this one test so the
        // document exists, then confirm it lists routes from more than one
        // module, proving the explicit IFeatureModule[] array in Program.cs
        // actually wired every module's MapEndpoints call.
        await using var devFactory = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var client = devFactory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(body);
        var paths = doc.RootElement.GetProperty("paths");

        var pathNames = paths.EnumerateObject().Select(p => p.Name).ToList();

        // One route per module — proves every slice's endpoints made it into
        // the document, not just whichever module happens to be registered first.
        pathNames.Should().Contain(p => p.Contains("/whoami"), "Users module");
        pathNames.Should().Contain(p => p.Contains("/dashboard/stats"), "Dashboard module");
        pathNames.Should().Contain(p => p.StartsWith("/groups"), "Groups module");
        pathNames.Should().Contain(p => p.StartsWith("/push"), "Notifications module");
        pathNames.Should().Contain(p => p.StartsWith("/auth"), "Auth module");
    }
}
