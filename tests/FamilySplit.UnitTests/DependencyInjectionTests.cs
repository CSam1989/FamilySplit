using FamilySplit.Application;
using FamilySplit.Application.Activities;
using FamilySplit.Application.Admin;
using FamilySplit.Application.Audit;
using FamilySplit.Application.Auth;
using FamilySplit.Application.Core;
using FamilySplit.Application.Dashboard;
using FamilySplit.Application.Expenses;
using FamilySplit.Application.Families;
using FamilySplit.Application.Groups;
using FamilySplit.Application.Push;
using FamilySplit.Application.Settlements;
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
    public void AddFamilySplitApplication_RegistersAllExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddFamilySplitApplication();

        // Assert
        result.Should().BeSameAs(services);

        services.Should().Contain(sd => sd.ServiceType == typeof(AuditService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(AdminService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(FamilyService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(GroupService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(ParticipantSeeder) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(ActivityService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(ExpenseService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(SettlementService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(DashboardService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(RefreshTokenService) && sd.Lifetime == ServiceLifetime.Scoped);
        services.Should().Contain(sd => sd.ServiceType == typeof(PushNotificationService) && sd.Lifetime == ServiceLifetime.Scoped);
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
