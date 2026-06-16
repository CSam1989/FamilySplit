using FamilySplit.Features.Users;
using FamilySplit.Features.Users.WhoAmI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Users slice. Replaces the old
/// <c>MapUserEndpoints</c> extension test now that registration and routing live
/// in <see cref="UsersModule"/>.
/// </summary>
public class UsersEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new UsersModule().RegisterServices(builder.Services, builder.Configuration);
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

    [Fact]
    public void RegisterServices_RegistersHandlerAsScoped()
    {
        var services = new ServiceCollection();
        new UsersModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(WhoAmIQueryHandler) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void MapEndpoints_RegistersExactlyOneEndpoint()
    {
        var app = CreateApp();

        new UsersModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(1);
    }

    [Fact]
    public void MapEndpoints_RegistersWhoamiGetEndpoint()
    {
        var app = CreateApp();

        new UsersModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.RoutePattern.RawText == "/whoami"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new UsersModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
