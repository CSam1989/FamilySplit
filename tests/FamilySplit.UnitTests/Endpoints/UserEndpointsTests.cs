using FamilySplit.Api.Endpoints;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class UserEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("user-test-" + Guid.NewGuid()));
        var app = builder.Build();
        return app;
    }

    [Fact]
    public void MapUserEndpoints_RegistersWhoamiGetEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapUserEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/whoami"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapUserEndpoints_RegistersExactlyOneEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapUserEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().HaveCount(1);
    }

    [Fact]
    public void MapUserEndpoints_ReturnsEndpointRouteBuilder()
    {
        // Arrange
        var app = CreateApp();

        // Act
        var result = app.MapUserEndpoints();

        // Assert
        result.Should().BeSameAs(app);
    }
}
