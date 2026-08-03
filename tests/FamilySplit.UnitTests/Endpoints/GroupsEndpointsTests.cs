using FamilySplit.Features.Groups;
using FamilySplit.Features.Groups.Create;
using FamilySplit.Features.Groups.Data;
using FamilySplit.Features.Groups.GetDetail;
using FamilySplit.Features.Groups.Join;
using FamilySplit.Features.Groups.Leave;
using FamilySplit.Features.Groups.List;
using FamilySplit.Features.Groups.RegenerateInviteCode;
using FamilySplit.Features.Groups.Update;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Groups slice (mirrors
/// <see cref="DashboardEndpointsTests"/>). Verifies that <see cref="GroupsModule"/> registers the
/// data seam + every handler as Scoped and maps the seven routes with the correct verbs / patterns.
/// </summary>
public class GroupsEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new GroupsModule().RegisterServices(builder.Services, builder.Configuration);
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
    [InlineData(typeof(IGroupData))]
    [InlineData(typeof(ListGroupsQueryHandler))]
    [InlineData(typeof(GetGroupDetailQueryHandler))]
    [InlineData(typeof(CreateGroupCommandHandler))]
    [InlineData(typeof(UpdateGroupCommandHandler))]
    [InlineData(typeof(JoinGroupCommandHandler))]
    [InlineData(typeof(LeaveGroupCommandHandler))]
    [InlineData(typeof(RegenerateInviteCodeCommandHandler))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new GroupsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersGroupDataImplementationAsGroupData()
    {
        var services = new ServiceCollection();
        new GroupsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IGroupData)
            && d.ImplementationType == typeof(GroupData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        new GroupsModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(CreateGroupCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(UpdateGroupCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(JoinGroupCommandValidator));
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlySevenEndpoints()
    {
        var app = CreateApp();

        new GroupsModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(7);
    }

    [Theory]
    [InlineData("/groups/", "GET")]              // List
    [InlineData("/groups/", "POST")]             // Create
    [InlineData("/groups/join", "POST")]         // Join
    [InlineData("/groups/{groupId:guid}", "GET")]   // GetDetail
    [InlineData("/groups/{groupId:guid}", "PUT")]   // Update
    [InlineData("/groups/{groupId:guid}/invite-code", "POST")] // Regenerate
    [InlineData("/groups/{groupId:guid}/leave", "DELETE")]     // Leave
    public void MapEndpoints_RegistersRouteWithVerb(string rawPattern, string verb)
    {
        var app = CreateApp();

        new GroupsModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == rawPattern
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(verb));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsCarryGroupsTag()
    {
        var app = CreateApp();

        new GroupsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Groups"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new GroupsModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
