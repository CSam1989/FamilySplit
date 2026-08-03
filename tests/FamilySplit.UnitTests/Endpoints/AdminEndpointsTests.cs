using FamilySplit.Features.Admin;
using FamilySplit.Features.Admin.AddFamilyMember;
using FamilySplit.Features.Admin.AddFamilyToGroup;
using FamilySplit.Features.Admin.CreateFamily;
using FamilySplit.Features.Admin.Data;
using FamilySplit.Features.Admin.DeleteGroup;
using FamilySplit.Features.Admin.GetFamily;
using FamilySplit.Features.Admin.ListFamilies;
using FamilySplit.Features.Admin.RemoveFamilyFromGroup;
using FamilySplit.Features.Admin.RemoveFamilyMember;
using FamilySplit.Features.Admin.UpdateFamilyMember;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Admin slice (mirrors the other slice endpoint tests).
/// Verifies that <see cref="AdminModule"/> registers the data seam + every handler as Scoped and maps
/// the nine routes with the correct verbs / patterns.
/// </summary>
public class AdminEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new AdminModule().RegisterServices(builder.Services, builder.Configuration);
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
    [InlineData(typeof(IAdminData))]
    [InlineData(typeof(ListFamiliesQueryHandler))]
    [InlineData(typeof(GetFamilyQueryHandler))]
    [InlineData(typeof(CreateFamilyCommandHandler))]
    [InlineData(typeof(AddFamilyMemberCommandHandler))]
    [InlineData(typeof(UpdateFamilyMemberCommandHandler))]
    [InlineData(typeof(RemoveFamilyMemberCommandHandler))]
    [InlineData(typeof(DeleteGroupCommandHandler))]
    [InlineData(typeof(AddFamilyToGroupCommandHandler))]
    [InlineData(typeof(RemoveFamilyFromGroupCommandHandler))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new AdminModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersAdminDataImplementationAsAdminData()
    {
        var services = new ServiceCollection();
        new AdminModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IAdminData)
            && d.ImplementationType == typeof(AdminData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        new AdminModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(CreateFamilyCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(AddFamilyMemberCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(UpdateFamilyMemberCommandValidator));
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlyNineEndpoints()
    {
        var app = CreateApp();

        new AdminModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(9);
    }

    [Theory]
    [InlineData("GET", "/admin/families")]
    [InlineData("POST", "/admin/families")]
    [InlineData("GET", "/admin/families/{familyId:guid}")]
    [InlineData("POST", "/admin/families/{familyId:guid}/members")]
    [InlineData("PUT", "/admin/families/{familyId:guid}/members/{memberId:guid}")]
    [InlineData("DELETE", "/admin/families/{familyId:guid}/members/{memberId:guid}")]
    [InlineData("DELETE", "/admin/groups/{groupId:guid}")]
    [InlineData("POST", "/admin/groups/{groupId:guid}/families")]
    [InlineData("DELETE", "/admin/groups/{groupId:guid}/families/{familyId:guid}")]
    public void MapEndpoints_RegistersRouteWithVerb(string method, string route)
    {
        var app = CreateApp();

        new AdminModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == route
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsCarryAdminTag()
    {
        var app = CreateApp();

        new AdminModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Admin"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new AdminModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
