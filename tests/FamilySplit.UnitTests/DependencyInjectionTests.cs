using FamilySplit.Common;
using FamilySplit.Common.Auditing;
using FamilySplit.Common.Security;
using FamilySplit.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace FamilySplit.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddFamilySplitCommon_RegistersAllExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFamilySplitCommon();

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(AuditService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(GroupMembershipGuard) && sd.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddFamilySplitInfrastructure_MissingConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => services.AddFamilySplitInfrastructure(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Missing connection string 'Postgres'*");
    }

    [Fact]
    public void AddFamilySplitInfrastructure_WithConnectionString_RegistersDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test",
            })
            .Build();

        // Act
        var result = services.AddFamilySplitInfrastructure(configuration);

        // Assert
        result.Should().BeSameAs(services);
        services.Should().Contain(sd => sd.ServiceType == typeof(AppDbContext));
    }

    [Fact]
    public void AddFamilySplitInfrastructure_NullEnvironment_RegistersDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test",
            })
            .Build();

        // Act
        var result = services.AddFamilySplitInfrastructure(configuration, environment: null);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddFamilySplitInfrastructure_ProductionEnvironment_RegistersDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test",
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Production");

        // Act
        var result = services.AddFamilySplitInfrastructure(configuration, environment.Object);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddFamilySplitInfrastructure_DevelopmentEnvironment_RegistersDbContext()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test",
            })
            .Build();
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        var result = services.AddFamilySplitInfrastructure(configuration, environment.Object);

        // Assert
        result.Should().BeSameAs(services);
    }
}
