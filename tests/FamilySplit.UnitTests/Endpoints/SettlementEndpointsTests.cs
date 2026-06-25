using FamilySplit.Features.Settlements;
using FamilySplit.Features.Settlements.ConfirmReceived;
using FamilySplit.Features.Settlements.ConfirmSent;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Generate;
using FamilySplit.Features.Settlements.GetBalances;
using FamilySplit.Features.Settlements.GetDetail;
using FamilySplit.Features.Settlements.List;
using FamilySplit.Features.Settlements.ListForGroup;
using FamilySplit.Features.Settlements.ListMyPending;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Settlements slice (mirrors <see cref="ActivitiesEndpointsTests"/>).
/// Verifies that <see cref="SettlementsModule"/> registers the data seam + every handler as Scoped and
/// maps the eight routes with the correct verbs / patterns. The slice has no validators (commands take
/// only route ids).
/// </summary>
public class SettlementsEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new SettlementsModule().RegisterServices(builder.Services, builder.Configuration);
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
    [InlineData(typeof(ISettlementData))]
    [InlineData(typeof(GetBalancesQueryHandler))]
    [InlineData(typeof(ListSettlementsQueryHandler))]
    [InlineData(typeof(GetSettlementDetailQueryHandler))]
    [InlineData(typeof(ListForGroupQueryHandler))]
    [InlineData(typeof(ListMyPendingQueryHandler))]
    [InlineData(typeof(GenerateSettlementsCommandHandler))]
    [InlineData(typeof(ConfirmSentCommandHandler))]
    [InlineData(typeof(ConfirmReceivedCommandHandler))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new SettlementsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersSettlementDataImplementationAsSettlementData()
    {
        var services = new ServiceCollection();
        new SettlementsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(ISettlementData)
            && d.ImplementationType == typeof(SettlementData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlyEightEndpoints()
    {
        var app = CreateApp();

        new SettlementsModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(8);
    }

    [Theory]
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/settlements/", "GET")]                          // List
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/settlements/", "POST")]                         // Generate
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}", "GET")]       // GetDetail
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}/confirm-sent", "POST")]
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/settlements/{settlementId:guid}/confirm-received", "POST")]
    [InlineData("/settlements/pending", "GET")]                                                                      // ListMyPending
    [InlineData("/groups/{groupId:guid}/settlements", "GET")]                                                        // ListForGroup
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/balances/", "GET")]                             // GetBalances
    public void MapEndpoints_RegistersRouteWithVerb(string rawPattern, string verb)
    {
        var app = CreateApp();

        new SettlementsModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == rawPattern
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(verb));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsCarrySettlementsTag()
    {
        var app = CreateApp();

        new SettlementsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Settlements"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new SettlementsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
