using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Activities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class ActivityEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ActivityService>(sp =>
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
    public void MapActivityEndpoints_Called_ReturnsTheSameWebApplication()
    {
        var app = CreateApp();

        var result = app.MapActivityEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapActivityEndpoints_Called_RegistersEightEndpoints()
    {
        var app = CreateApp();

        app.MapActivityEndpoints();

        GetEndpoints(app).Should().HaveCount(8);
    }

    [Theory]
    [InlineData("GET", "/groups/{groupId:guid}/activities/")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/")]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}")]
    [InlineData("PUT", "/groups/{groupId:guid}/activities/{activityId:guid}")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/close")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/sub-activities")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/participants")]
    [InlineData("DELETE", "/groups/{groupId:guid}/activities/{activityId:guid}/participants/{memberId:guid}")]
    public void MapActivityEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapActivityEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapActivityEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapActivityEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
