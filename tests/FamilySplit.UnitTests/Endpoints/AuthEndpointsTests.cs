using FamilySplit.Features.Auth;
using FamilySplit.Features.Auth.Data;
using FamilySplit.Features.Auth.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FamilySplit.UnitTests.Endpoints;

/// <summary>
/// Per-module DI + route-mapping test for the Auth slice (mirrors the other slice
/// endpoint tests). Auth has no I{Slice}Data / command-query split — it verifies
/// instead that RegisterServices wires JwtBearer auth, the "auth" rate-limit
/// policy, the google-oauth HttpClient, and the slice's own services, and that
/// MapEndpoints maps the four routes unchanged from the pre-migration shape.
/// </summary>
public class AuthEndpointsTests
{
    private static readonly Dictionary<string, string?> ValidJwtConfig = new()
    {
        ["Jwt:SigningKey"] = "this-is-a-test-signing-key-at-least-32-chars!!",
        ["Jwt:Issuer"] = "familysplit-test",
        ["Jwt:Audience"] = "familysplit-test-client",
    };

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(ValidJwtConfig);
        builder.Services.AddDataProtection();
        new AuthModule().RegisterServices(builder.Services, builder.Configuration);
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

    private static IServiceCollection BuildRegisteredServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ValidJwtConfig).Build();
        services.AddDataProtection();
        new AuthModule().RegisterServices(services, configuration);
        return services;
    }

    // ── RegisterServices ──────────────────────────────────────────────────────────

    [Fact]
    public void RegisterServices_MissingSigningKey_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var act = () => new AuthModule().RegisterServices(services, configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:SigningKey*");
    }

    [Fact]
    public void RegisterServices_ShortSigningKey_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "too-short" })
            .Build();

        var act = () => new AuthModule().RegisterServices(services, configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");
    }

    [Fact]
    public void RegisterServices_RegistersJwtFactoryAsSingleton()
    {
        var services = BuildRegisteredServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(JwtFactory) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterServices_RegistersPkceFlowAsSingleton()
    {
        var services = BuildRegisteredServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(PkceFlow) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterServices_RegistersOAuthDataAsScoped()
    {
        var services = BuildRegisteredServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(OAuthData) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void RegisterServices_RegistersRefreshTokenDataAsScoped()
    {
        var services = BuildRegisteredServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(RefreshTokenData) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task RegisterServices_RegistersJwtBearerAuthentication()
    {
        var provider = BuildRegisteredServices().BuildServiceProvider();

        var schemeProvider = provider.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

        scheme.Should().NotBeNull();
    }

    [Fact]
    public void RegisterServices_RegistersGoogleOAuthHttpClient()
    {
        var provider = BuildRegisteredServices().BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("google-oauth");

        client.Should().NotBeNull();
    }

    [Fact]
    public void RegisterServices_ComposesWithHostsOwnAddRateLimiterCall_AndRegistersAuthPolicy()
    {
        // Mirrors Program.cs: the host's own AddRateLimiter call (global limiter +
        // OnRejected) runs first, then AuthModule.RegisterServices adds a second,
        // independent AddRateLimiter call for the "auth" named policy. ASP.NET
        // Core's options infrastructure runs every registered
        // IConfigureOptions<RateLimiterOptions> delegate, in registration order,
        // against the same RateLimiterOptions instance — so both configurations
        // must survive. RateLimiterOptions.AddPolicy throws ArgumentException
        // if the same policy name is added twice, which is used here as the public
        // API surface's only observable proof that AuthModule's AddPolicy call for
        // "auth" actually ran: forcing the resolution and adding "auth" again must
        // throw "already exists", proving the first (AuthModule's) registration won.
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ValidJwtConfig).Build();
        var hostDelegateRan = false;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            hostDelegateRan = true;
        });
        services.AddDataProtection();
        new AuthModule().RegisterServices(services, configuration);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>().Value;

        hostDelegateRan.Should().BeTrue("resolving IOptions<RateLimiterOptions> must run every registered configure delegate, including the host's");
        options.RejectionStatusCode.Should().Be(429, "the host's own AddRateLimiter configuration must survive AuthModule's separate AddRateLimiter call");

        var act = () => options.AddPolicy(FamilySplit.Common.Routing.RateLimitPolicies.Auth,
            _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("probe"));
        act.Should().Throw<ArgumentException>().WithMessage("*already exists*",
            "AuthModule's AddPolicy(\"auth\", ...) must already have registered this policy name");
    }

    // ── MapEndpoints ──────────────────────────────────────────────────────────────

    [Fact]
    public void MapEndpoints_RegistersExactlyFourEndpoints()
    {
        var app = CreateApp();

        new AuthModule().MapEndpoints(app);

        GetEndpoints(app).Should().HaveCount(4);
    }

    [Theory]
    [InlineData("GET", "/auth/login/{provider}")]
    [InlineData("GET", "/auth/callback/{provider}")]
    [InlineData("POST", "/auth/refresh")]
    [InlineData("POST", "/auth/logout")]
    public void MapEndpoints_RegistersRouteWithVerb(string method, string route)
    {
        var app = CreateApp();

        new AuthModule().MapEndpoints(app);

        GetEndpoints(app).Should().Contain(e =>
            e.RoutePattern.RawText == route
            && e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsAllowAnonymous()
    {
        var app = CreateApp();

        new AuthModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.Should().Contain(m => m.GetType().Name == "AllowAnonymousAttribute"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsCarryAuthTag()
    {
        var app = CreateApp();

        new AuthModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e =>
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags.Should().Contain("Auth"));
    }

    [Fact]
    public void MapEndpoints_AllEndpointsHaveDisplayName()
    {
        var app = CreateApp();

        new AuthModule().MapEndpoints(app);

        var endpoints = GetEndpoints(app);
        endpoints.Should().NotBeEmpty();
        endpoints.Should().AllSatisfy(e => e.DisplayName.Should().NotBeNullOrEmpty());
    }
}
