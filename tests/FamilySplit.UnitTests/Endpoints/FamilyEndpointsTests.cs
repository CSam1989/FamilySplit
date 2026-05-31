using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Families;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class FamilyEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<FamilyService>(sp =>
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
    public void MapFamilyEndpoints_Called_ReturnsTheSameWebApplication()
    {
        var app = CreateApp();

        var result = app.MapFamilyEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapFamilyEndpoints_Called_RegistersFiveEndpoints()
    {
        var app = CreateApp();

        app.MapFamilyEndpoints();

        GetEndpoints(app).Should().HaveCount(5);
    }

    [Theory]
    [InlineData("GET", "/families/mine/")]
    [InlineData("PUT", "/families/mine/")]
    [InlineData("POST", "/families/mine/members")]
    [InlineData("PUT", "/families/mine/members/{memberId:guid}")]
    [InlineData("DELETE", "/families/mine/members/{memberId:guid}")]
    public void MapFamilyEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapFamilyEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapFamilyEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapFamilyEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
