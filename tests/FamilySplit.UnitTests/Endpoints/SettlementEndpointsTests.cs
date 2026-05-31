using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Settlements;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class SettlementEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<SettlementService>(sp =>
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
    public void MapSettlementEndpoints_Called_ReturnsTheSameWebApplication()
    {
        var app = CreateApp();

        var result = app.MapSettlementEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapSettlementEndpoints_Called_RegistersEightEndpoints()
    {
        var app = CreateApp();

        app.MapSettlementEndpoints();

        GetEndpoints(app).Should().HaveCount(8);
    }

    [Theory]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/settlements/")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/settlements/")]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}/confirm-sent")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}/confirm-received")]
    [InlineData("GET", "/settlements/pending")]
    [InlineData("GET", "/groups/{groupId:guid}/settlements")]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/balances/")]
    public void MapSettlementEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapSettlementEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapSettlementEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapSettlementEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
