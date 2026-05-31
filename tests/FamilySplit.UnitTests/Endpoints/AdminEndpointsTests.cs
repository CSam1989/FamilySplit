using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Admin;
using FamilySplit.Application.Families;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class AdminEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("admin-test"));
        builder.Services.AddScoped<AdminService>();
        builder.Services.AddScoped<CreateFamilyValidator>();
        builder.Services.AddScoped<AddFamilyMemberValidator>();
        builder.Services.AddScoped<UpdateFamilyMemberValidator>();
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
    public void MapAdminEndpoints_ReturnsWebApplication()
    {
        var app = CreateApp();

        var result = app.MapAdminEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapAdminEndpoints_RegistersNineEndpoints()
    {
        var app = CreateApp();

        app.MapAdminEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().HaveCount(9);
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
    public void MapAdminEndpoints_RegistersExpectedEndpoint(string method, string route)
    {
        var app = CreateApp();

        app.MapAdminEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e => e.RoutePattern.RawText == route
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public void MapAdminEndpoints_AllEndpointsHaveAdminTag()
    {
        var app = CreateApp();

        app.MapAdminEndpoints();

        var endpoints = GetEndpoints(app);
        foreach (var endpoint in endpoints)
        {
            var tags = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>();
            tags.Should().NotBeNull();
            tags!.Tags.Should().Contain("Admin");
        }
    }
}
