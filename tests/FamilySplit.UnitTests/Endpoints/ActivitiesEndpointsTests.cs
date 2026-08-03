using FamilySplit.Features.Activities;
using FamilySplit.Features.Activities.AddParticipant;
using FamilySplit.Features.Activities.Close;
using FamilySplit.Features.Activities.Create;
using FamilySplit.Features.Activities.CreateSubActivity;
using FamilySplit.Features.Activities.Data;
using FamilySplit.Features.Activities.GetDetail;
using FamilySplit.Features.Activities.List;
using FamilySplit.Features.Activities.RemoveParticipant;
using FamilySplit.Features.Activities.Update;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Activities slice (mirrors <see cref="GroupsEndpointsTests"/>).
/// Verifies that <see cref="ActivitiesModule"/> registers the data seam + every handler as Scoped and
/// maps the eight routes with the correct verbs / patterns.
/// </summary>
public class ActivitiesEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new ActivitiesModule().RegisterServices(builder.Services, builder.Configuration);
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
    [InlineData(typeof(IActivityData))]
    [InlineData(typeof(ListActivitiesQueryHandler))]
    [InlineData(typeof(GetActivityDetailQueryHandler))]
    [InlineData(typeof(CreateActivityCommandHandler))]
    [InlineData(typeof(CreateSubActivityCommandHandler))]
    [InlineData(typeof(UpdateActivityCommandHandler))]
    [InlineData(typeof(CloseActivityCommandHandler))]
    [InlineData(typeof(AddParticipantCommandHandler))]
    [InlineData(typeof(RemoveParticipantCommandHandler))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new ActivitiesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersActivityDataImplementationAsActivityData()
    {
        var services = new ServiceCollection();
        new ActivitiesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IActivityData)
            && d.ImplementationType == typeof(ActivityData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        new ActivitiesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(CreateActivityCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(UpdateActivityCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(AddParticipantCommandValidator));
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlyEightEndpoints()
    {
        var app = CreateApp();

        new ActivitiesModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(8);
    }

    [Theory]
    [InlineData("/groups/{groupId:guid}/activities/", "GET")]                                          // List
    [InlineData("/groups/{groupId:guid}/activities/", "POST")]                                         // Create
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}", "GET")]                         // GetDetail
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}", "PUT")]                         // Update
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/close", "POST")]                  // Close
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/sub-activities", "POST")]         // CreateSub
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/participants", "POST")]           // AddParticipant
    [InlineData("/groups/{groupId:guid}/activities/{activityId:guid}/participants/{memberId:guid}", "DELETE")] // RemoveParticipant
    public void MapEndpoints_RegistersRouteWithVerb(string rawPattern, string verb)
    {
        var app = CreateApp();

        new ActivitiesModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == rawPattern
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(verb));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsCarryActivitiesTag()
    {
        var app = CreateApp();

        new ActivitiesModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Activities"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new ActivitiesModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
