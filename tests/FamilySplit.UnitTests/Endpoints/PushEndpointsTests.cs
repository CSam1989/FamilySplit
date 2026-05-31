using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Push;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class PushEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<PushNotificationService>(sp =>
            throw new InvalidOperationException("Should not be resolved in unit tests"));
        var app = builder.Build();
        return app;
    }

    private static List<RouteEndpoint> GetEndpoints(WebApplication app)
    {
        var endpointDataSource = app as IEndpointRouteBuilder;
        return endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void MapPushEndpoints_Called_RegistersThreeEndpoints()
    {
        var app = CreateApp();

        app.MapPushEndpoints();

        GetEndpoints(app).Should().HaveCount(3);
    }

    [Theory]
    [InlineData("GET", "/push/vapid-public-key")]
    [InlineData("POST", "/push/subscribe")]
    [InlineData("DELETE", "/push/unsubscribe")]
    public void MapPushEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapPushEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapPushEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapPushEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void MapPushEndpoints_VapidPublicKeyEndpoint_AllowsAnonymous()
    {
        var app = CreateApp();

        app.MapPushEndpoints();

        var endpoints = GetEndpoints(app);
        var vapidEndpoint = endpoints.Single(e =>
            e.DisplayName!.Contains("/push/vapid-public-key"));
        vapidEndpoint.Metadata.Should().Contain(m =>
            m.GetType().Name == "AllowAnonymousAttribute");
    }
}
