using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Expenses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class ExpenseEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<ExpenseService>(sp =>
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
    public void MapExpenseEndpoints_Called_ReturnsTheSameWebApplication()
    {
        var app = CreateApp();

        var result = app.MapExpenseEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapExpenseEndpoints_Called_RegistersFiveEndpoints()
    {
        var app = CreateApp();

        app.MapExpenseEndpoints();

        GetEndpoints(app).Should().HaveCount(5);
    }

    [Theory]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/")]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    [InlineData("PUT", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    [InlineData("DELETE", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    public void MapExpenseEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapExpenseEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapExpenseEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapExpenseEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
