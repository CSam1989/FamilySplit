using System.IdentityModel.Tokens.Jwt;
using FamilySplit.Api.Auth;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace FamilySplit.UnitTests.Auth;

public class JwtFactoryTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var defaults = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "this-is-a-test-signing-key-at-least-32-chars!!",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:LifetimeMinutes"] = "30",
        };

        if (overrides is not null)
        {
            foreach (var kv in overrides)
            {
                defaults[kv.Key] = kv.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .Build();
    }

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "test@example.com",
        DisplayName = "Test User",
        Provider = Provider.Google,
    };

    [Fact]
    public void Constructor_WithConfig_DoesNotThrow()
    {
        var config = BuildConfig();
        var factory = new JwtFactory(config);
        factory.Should().NotBeNull();
    }

    [Fact]
    public void LifetimeMinutes_Configured_ReturnsConfiguredValue()
    {
        var factory = new JwtFactory(BuildConfig());
        factory.LifetimeMinutes.Should().Be(30);
    }

    [Fact]
    public void LifetimeMinutes_Missing_ReturnsDefault15()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "key",
            })
            .Build();

        new JwtFactory(config).LifetimeMinutes.Should().Be(15);
    }

    [Fact]
    public void LifetimeMinutes_InvalidValue_ReturnsDefault15()
    {
        var factory = new JwtFactory(BuildConfig(new() { ["Jwt:LifetimeMinutes"] = "notanumber" }));
        factory.LifetimeMinutes.Should().Be(15);
    }

    [Fact]
    public void Create_ValidUser_ReturnsValidJwt()
    {
        var user = CreateUser();
        var factory = new JwtFactory(BuildConfig());

        var token = factory.Create(user);

        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == user.DisplayName);
        jwt.Claims.Should().Contain(c => c.Type == "provider" && c.Value == user.Provider.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "jti");
    }

    [Fact]
    public void Create_MissingSigningKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "x",
            })
            .Build();

        var factory = new JwtFactory(config);

        var act = () => factory.Create(CreateUser());
        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKey*");
    }

    [Fact]
    public void Create_MissingIssuer_UsesDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "this-is-a-test-signing-key-at-least-32-chars!!",
            })
            .Build();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(new JwtFactory(config).Create(CreateUser()));
        jwt.Issuer.Should().Be("familysplit");
    }

    [Fact]
    public void Create_MissingAudience_UsesDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "this-is-a-test-signing-key-at-least-32-chars!!",
            })
            .Build();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(new JwtFactory(config).Create(CreateUser()));
        jwt.Audiences.Should().Contain("familysplit-client");
    }
}
