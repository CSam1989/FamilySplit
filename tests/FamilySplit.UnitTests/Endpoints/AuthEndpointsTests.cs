using FamilySplit.Api.Auth;
using FamilySplit.Api.Endpoints;
using FamilySplit.Application.Auth;
using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.UnitTests.Endpoints;

public class AuthEndpointsTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<PkceFlow>();
        builder.Services.AddSingleton<JwtFactory>();
        builder.Services.AddScoped<OAuthHandler>();
        builder.Services.AddScoped<RefreshTokenService>();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("test"));
        builder.Services.AddHttpClient();
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
    public void MapAuthEndpoints_Called_ReturnsTheSameWebApplication()
    {
        var app = CreateApp();

        var result = app.MapAuthEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapAuthEndpoints_Called_RegistersFourEndpoints()
    {
        var app = CreateApp();

        app.MapAuthEndpoints();

        GetEndpoints(app).Should().HaveCount(4);
    }

    [Theory]
    [InlineData("GET", "/auth/login/{provider}")]
    [InlineData("GET", "/auth/callback/{provider}")]
    [InlineData("POST", "/auth/refresh")]
    [InlineData("POST", "/auth/logout")]
    public void MapAuthEndpoints_Called_RegistersEndpoint(string httpMethod, string expectedPattern)
    {
        var app = CreateApp();

        app.MapAuthEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().Contain(e =>
            e.DisplayName!.Contains($"HTTP: {httpMethod} {expectedPattern}"));
    }

    [Fact]
    public void MapAuthEndpoints_Called_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        app.MapAuthEndpoints();

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        foreach (var endpoint in endpoints)
        {
            endpoint.DisplayName.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void MapAuthEndpoints_CalledTwice_RegistersEndpointsTwice()
    {
        var app = CreateApp();

        app.MapAuthEndpoints();
        app.MapAuthEndpoints();

        GetEndpoints(app).Should().HaveCount(8);
    }

    [Fact]
    public void MapAuthEndpoints_Called_AllEndpointsAllowAnonymous()
    {
        var app = CreateApp();

        app.MapAuthEndpoints();

        var endpoints = GetEndpoints(app);
        foreach (var endpoint in endpoints)
        {
            endpoint.Metadata.Should().Contain(m =>
                m.GetType().Name == "AllowAnonymousAttribute");
        }
    }
}
