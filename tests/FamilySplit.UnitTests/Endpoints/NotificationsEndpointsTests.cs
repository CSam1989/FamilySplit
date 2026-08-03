using FamilySplit.Common.Notifications;
using FamilySplit.Features.Notifications;
using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.GetVapidPublicKey;
using FamilySplit.Features.Notifications.Shared;
using FamilySplit.Features.Notifications.Subscribe;
using FamilySplit.Features.Notifications.Unsubscribe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Notifications slice (mirrors
/// <see cref="GroupsEndpointsTests"/> / <see cref="UsersEndpointsTests"/>). Verifies that
/// <see cref="NotificationsModule"/> registers the data seam, every handler, SignalR, and the
/// composite <see cref="INotificationService"/> as expected, and maps the three <c>/push</c> routes
/// plus the <c>/hubs/notifications</c> hub.
/// </summary>
public class NotificationsEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new NotificationsModule().RegisterServices(builder.Services, builder.Configuration);
        return builder.Build();
    }

    private static List<RouteEndpoint> GetEndpoints(WebApplication app)
    {
        var endpointDataSource = app as IEndpointRouteBuilder;
        return endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    // ── RegisterServices ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(IPushSubscriptionData))]
    [InlineData(typeof(GetVapidPublicKeyQueryHandler))]
    [InlineData(typeof(SubscribeCommandHandler))]
    [InlineData(typeof(UnsubscribeCommandHandler))]
    [InlineData(typeof(VapidPushSender))]
    [InlineData(typeof(INotificationService))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new NotificationsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersPushSubscriptionDataImplementation()
    {
        var services = new ServiceCollection();
        new NotificationsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IPushSubscriptionData)
            && d.ImplementationType == typeof(PushSubscriptionData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersSignalRNotificationServiceAsINotificationService()
    {
        var services = new ServiceCollection();
        new NotificationsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(INotificationService)
            && d.ImplementationType == typeof(SignalRNotificationService)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        new NotificationsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(SubscribeCommandValidator));
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersThePushRoutesAndTheHub()
    {
        var app = CreateApp();

        new NotificationsModule().MapEndpoints(app);

        // 3 push routes + the SignalR hub's endpoint(s) (the hub negotiate handshake adds its own
        // route alongside the connection endpoint, so this is >= 4 rather than an exact count).
        GetEndpoints(app).Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Theory]
    [InlineData("/push/vapid-public-key", "GET")]
    [InlineData("/push/subscribe", "POST")]
    [InlineData("/push/unsubscribe", "DELETE")]
    public void MapEndpoints_RegistersRouteWithVerb(string rawPattern, string verb)
    {
        var app = CreateApp();

        new NotificationsModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == rawPattern
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(verb));
    }

    [Fact]
    public void MapEndpoints_VapidPublicKeyEndpoint_AllowsAnonymous()
    {
        var app = CreateApp();

        new NotificationsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        var vapidEndpoint = endpoints.Single(e => e.RoutePattern.RawText == "/push/vapid-public-key");
        vapidEndpoint.Metadata.Should().Contain(m => m.GetType().Name == "AllowAnonymousAttribute");
    }

    [Fact]
    public void MapEndpoints_MapsNotificationsHub()
    {
        var app = CreateApp();

        new NotificationsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/hubs/notifications");
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new NotificationsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
