using System.Net.Http.Json;
using FamilySplit.IntegrationTests.Infrastructure;

namespace FamilySplit.IntegrationTests.Notifications;

/// <summary>
/// Testcontainers coverage for the Notifications slice: the <c>/push</c> routes and — through
/// them — the <c>PushSubscriptionData</c> gateway (ADR-001's write-side data access). No
/// integration tests existed for Push before this phase.
/// </summary>
[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class NotificationsEndpointsTests : IntegrationTestBase
{
    public NotificationsEndpointsTests(PostgresContainerFixture fixture) : base(fixture) { }

    // ── GetVapidPublicKey (query, AllowAnonymous) ──────────────────────────────────

    [Fact]
    public async Task GetVapidPublicKey_Configured_ReturnsKey_EvenWithoutAuth()
    {
        var ct = TestContext.Current.CancellationToken;

        using var configuredFactory = Factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Push:Vapid:PublicKey", "test-vapid-public-key");
        });
        using var anonymousClient = configuredFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await anonymousClient.GetAsync("/push/vapid-public-key", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VapidPublicKeyResponse>(ct);
        body!.PublicKey.Should().Be("test-vapid-public-key");
    }

    [Fact]
    public async Task GetVapidPublicKey_NotConfigured_Returns500()
    {
        // Documents the legacy (preserved) behaviour: a missing VAPID key is an
        // uncaught InvalidOperationException, not a handled 422/403.
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.GetAsync("/push/vapid-public-key", ct);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── Subscribe ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_ValidRequest_Returns204_AndPersistsRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var endpoint = $"https://push.example.com/{Guid.NewGuid():N}";

        var response = await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint,
            p256dh = "p256dh-key",
            auth = "auth-secret",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT user_id, p256dh, auth FROM push_subscriptions WHERE endpoint = @endpoint";
        cmd.Parameters.AddWithValue("endpoint", endpoint);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        (await reader.ReadAsync(ct)).Should().BeTrue();
        reader.GetGuid(0).Should().Be(CallerId);
        reader.GetString(1).Should().Be("p256dh-key");
        reader.GetString(2).Should().Be("auth-secret");
    }

    [Fact]
    public async Task Subscribe_ExistingEndpoint_UpdatesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var endpoint = $"https://push.example.com/{Guid.NewGuid():N}";

        await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint,
            p256dh = "old-key",
            auth = "old-secret",
        }, ct);

        var response = await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint,
            p256dh = "new-key",
            auth = "new-secret",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*), MAX(p256dh) FROM push_subscriptions WHERE endpoint = @endpoint";
        cmd.Parameters.AddWithValue("endpoint", endpoint);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        reader.GetInt64(0).Should().Be(1);
        reader.GetString(1).Should().Be("new-key");
    }

    [Fact]
    public async Task Subscribe_NonHttpsEndpoint_Returns422OnEndpointField()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint = "http://push.example.com/insecure",
            p256dh = "p256dh-key",
            auth = "auth-secret",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Endpoint")
            .EnumerateArray().First().GetString()
            .Should().Be("Endpoint must be a valid https URL.");
    }

    [Fact]
    public async Task Subscribe_EmptyEndpoint_Returns422OnEndpointField()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint = "",
            p256dh = "p256dh-key",
            auth = "auth-secret",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("errors").GetProperty("Endpoint")
            .EnumerateArray().First().GetString()
            .Should().Be("Endpoint is required and must be at most 2048 characters.");
    }

    [Fact]
    public async Task Subscribe_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var anonymousClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var response = await anonymousClient.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint = "https://push.example.com/abc",
            p256dh = "p256dh-key",
            auth = "auth-secret",
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Unsubscribe ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unsubscribe_ExistingSubscription_Returns204_AndRemovesRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var endpoint = $"https://push.example.com/{Guid.NewGuid():N}";
        await Client.PostAsJsonAsync("/push/subscribe", new
        {
            endpoint,
            p256dh = "p256dh-key",
            auth = "auth-secret",
        }, ct);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/push/unsubscribe")
        {
            Content = JsonContent.Create(new { endpoint }),
        };
        var response = await Client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM push_subscriptions WHERE endpoint = @endpoint";
        cmd.Parameters.AddWithValue("endpoint", endpoint);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        count.Should().Be(0);
    }

    [Fact]
    public async Task Unsubscribe_NoMatch_Returns204()
    {
        var ct = TestContext.Current.CancellationToken;

        var request = new HttpRequestMessage(HttpMethod.Delete, "/push/unsubscribe")
        {
            Content = JsonContent.Create(new { endpoint = "https://push.example.com/nonexistent" }),
        };
        var response = await Client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unsubscribe_Unauthenticated_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;
        using var anonymousClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        var request = new HttpRequestMessage(HttpMethod.Delete, "/push/unsubscribe")
        {
            Content = JsonContent.Create(new { endpoint = "https://push.example.com/abc" }),
        };
        var response = await anonymousClient.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record VapidPublicKeyResponse(string PublicKey);
}
