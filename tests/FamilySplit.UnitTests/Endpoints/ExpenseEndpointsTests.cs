using FamilySplit.Features.Expenses;
using FamilySplit.Features.Expenses.Create;
using FamilySplit.Features.Expenses.Delete;
using FamilySplit.Features.Expenses.GetDetail;
using FamilySplit.Features.Expenses.List;
using FamilySplit.Features.Expenses.Update;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Expenses slice. Replaces the old
/// <c>MapExpenseEndpoints</c> extension test now that registration and routing live
/// in <see cref="ExpensesModule"/>.
/// </summary>
public class ExpenseEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new ExpensesModule().RegisterServices(builder.Services, builder.Configuration);
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
    public void RegisterServices_RegistersAllHandlersAsScoped()
    {
        var services = new ServiceCollection();
        new ExpensesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        var handlerTypes = new[]
        {
            typeof(ListExpensesQueryHandler),
            typeof(GetExpenseDetailQueryHandler),
            typeof(CreateExpenseCommandHandler),
            typeof(UpdateExpenseCommandHandler),
            typeof(DeleteExpenseCommandHandler),
        };

        foreach (var t in handlerTypes)
            services.Should().Contain(d => d.ServiceType == t && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void MapEndpoints_RegistersFiveEndpoints()
    {
        var app = CreateApp();

        new ExpensesModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(5);
    }

    [Theory]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/")]
    [InlineData("POST", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/")]
    [InlineData("GET", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    [InlineData("PUT", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    [InlineData("DELETE", "/groups/{groupId:guid}/activities/{activityId:guid}/expenses/{expenseId:guid}")]
    public void MapEndpoints_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        new ExpensesModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new ExpensesModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }
}
