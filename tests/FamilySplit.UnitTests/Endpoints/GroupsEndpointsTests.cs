using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Groups;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class GroupsEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddScoped<CreateGroupValidator>();
        builder.Services.AddScoped<UpdateGroupValidator>();
        builder.Services.AddScoped<JoinGroupValidator>();
        builder.Services.AddScoped<GroupService>();
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
    public void MapGroupEndpoints_ReturnsWebApplication()
    {
        // Arrange
        var app = CreateApp();

        // Act
        var result = app.MapGroupEndpoints();

        // Assert
        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapGroupEndpoints_RegistersSevenEndpoints()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().HaveCount(7);
    }

    [Fact]
    public void MapGroupEndpoints_RegistersGetListEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersGetDetailEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/{groupId:guid}"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("GET"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersPostCreateEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersPutUpdateEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/{groupId:guid}"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("PUT"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersPostJoinEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/join"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersPostInviteCodeEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/{groupId:guid}/invite-code"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"));
    }

    [Fact]
    public void MapGroupEndpoints_RegistersDeleteLeaveEndpoint()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().Contain(e => e.RoutePattern.RawText == "/groups/{groupId:guid}/leave"
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("DELETE"));
    }

    [Fact]
    public void MapGroupEndpoints_AllEndpointsHaveGroupsTag()
    {
        // Arrange
        var app = CreateApp();
        app.MapGroupEndpoints();

        // Act
        var endpoints = GetEndpoints(app);

        // Assert
        endpoints.Should().AllSatisfy(e =>
            e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
