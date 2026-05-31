using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Families;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class FamilyMembersEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("fmtest"));
        builder.Services.AddScoped<FamilyService>();
        var app = builder.Build();
        return app;
    }

    [Fact]
    public void MapFamilyMemberEndpoints_RegistersProfileGetEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapFamilyMemberEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/users/me/profile"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapFamilyMemberEndpoints_RegistersExactlyOneEndpoint()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapFamilyMemberEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        endpoints.Should().HaveCount(1);
    }

    [Fact]
    public void MapFamilyMemberEndpoints_ReturnsWebApplication()
    {
        // Arrange
        var app = CreateApp();

        // Act
        var result = app.MapFamilyMemberEndpoints();

        // Assert
        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapFamilyMemberEndpoints_EndpointHasProfileTag()
    {
        // Arrange
        var app = CreateApp();

        // Act
        app.MapFamilyMemberEndpoints();

        // Assert
        var endpointDataSource = app as IEndpointRouteBuilder;
        var endpoints = endpointDataSource.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var profileEndpoint = endpoints.Single();
        var tagMetadata = profileEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>();
        tagMetadata.Should().NotBeNull();
        tagMetadata!.Tags.Should().Contain("Profile");
    }
}
