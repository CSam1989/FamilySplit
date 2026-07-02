using FamilySplit.Features.Families;
using FamilySplit.Features.Families.AddMember;
using FamilySplit.Features.Families.Data;
using FamilySplit.Features.Families.GetMyFamily;
using FamilySplit.Features.Families.GetMyProfile;
using FamilySplit.Features.Families.RemoveMember;
using FamilySplit.Features.Families.UpdateFamilyName;
using FamilySplit.Features.Families.UpdateMember;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Families slice (mirrors the other slice endpoint
/// tests). Verifies that <see cref="FamiliesModule"/> registers the data seam + every handler as
/// Scoped and maps the six routes (five under <c>/families/mine</c>, one under <c>/users/me</c>)
/// with the correct verbs / patterns.
/// </summary>
public class FamiliesEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        new FamiliesModule().RegisterServices(builder.Services, builder.Configuration);
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
    [InlineData(typeof(IFamilyData))]
    [InlineData(typeof(GetMyFamilyQueryHandler))]
    [InlineData(typeof(GetMyProfileQueryHandler))]
    [InlineData(typeof(UpdateFamilyNameCommandHandler))]
    [InlineData(typeof(AddMemberCommandHandler))]
    [InlineData(typeof(UpdateMemberCommandHandler))]
    [InlineData(typeof(RemoveMemberCommandHandler))]
    public void RegisterServices_RegistersServiceAsScoped(Type serviceType)
    {
        var services = new ServiceCollection();
        new FamiliesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == serviceType && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersFamilyDataImplementationAsFamilyData()
    {
        var services = new ServiceCollection();
        new FamiliesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IFamilyData)
            && d.ImplementationType == typeof(FamilyData)
            && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersValidators()
    {
        var services = new ServiceCollection();
        new FamiliesModule().RegisterServices(services, new ConfigurationBuilder().Build());

        services.Should().Contain(d => d.ServiceType == typeof(UpdateFamilyNameCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(AddMemberCommandValidator));
        services.Should().Contain(d => d.ServiceType == typeof(UpdateMemberCommandValidator));
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlySixEndpoints()
    {
        var app = CreateApp();

        new FamiliesModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(6);
    }

    [Theory]
    [InlineData("GET", "/families/mine/")]
    [InlineData("PUT", "/families/mine/")]
    [InlineData("POST", "/families/mine/members")]
    [InlineData("PUT", "/families/mine/members/{memberId:guid}")]
    [InlineData("DELETE", "/families/mine/members/{memberId:guid}")]
    [InlineData("GET", "/users/me/profile")]
    public void MapEndpoints_RegistersRouteWithVerb(string method, string route)
    {
        var app = CreateApp();

        new FamiliesModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == route
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public void MapEndpoints_FamilyRoutesCarryFamilyTag()
    {
        var app = CreateApp();

        new FamiliesModule().MapEndpoints(app);

        var familyEndpoints = GetEndpoints(app)
            .Where(e => e.RoutePattern.RawText!.StartsWith("/families/mine"));
        familyEndpoints.Should().NotBeEmpty();
        familyEndpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Family"));
    }

    [Fact]
    public void MapEndpoints_ProfileRouteCarriesProfileTag()
    {
        var app = CreateApp();

        new FamiliesModule().MapEndpoints(app);

        var profileEndpoint = GetEndpoints(app).Single(e => e.RoutePattern.RawText == "/users/me/profile");
        profileEndpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Profile");
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new FamiliesModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
