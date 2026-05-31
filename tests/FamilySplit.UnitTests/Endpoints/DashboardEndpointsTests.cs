using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Dashboard;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class DashboardEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("test"));
        builder.Services.AddScoped<DashboardService>();
        var app = builder.Build();
        return app;
    }

    [Fact]
    public void MapDashboardEndpoints_RegistersStatsGetEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapDashboardEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/dashboard/stats"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapDashboardEndpoints_RegistersExactlyOneEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapDashboardEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().HaveCount(1);
    }
}
