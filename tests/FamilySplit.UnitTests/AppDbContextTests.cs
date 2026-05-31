using FluentAssertions;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.UnitTests;

public class AppDbContextTests : IDisposable
{
    private readonly AppDbContext _sut;

    public AppDbContextTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _sut = new AppDbContext(options);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        _sut.Should().NotBeNull();
    }

    [Fact]
    public void Users_ShouldReturnDbSet()
    {
        _sut.Users.Should().NotBeNull();
    }

    [Fact]
    public void Families_ShouldReturnDbSet()
    {
        _sut.Families.Should().NotBeNull();
    }

    [Fact]
    public void FamilyMembers_ShouldReturnDbSet()
    {
        _sut.FamilyMembers.Should().NotBeNull();
    }

    [Fact]
    public void Groups_ShouldReturnDbSet()
    {
        _sut.Groups.Should().NotBeNull();
    }

    [Fact]
    public void GroupFamilies_ShouldReturnDbSet()
    {
        _sut.GroupFamilies.Should().NotBeNull();
    }

    [Fact]
    public void Activities_ShouldReturnDbSet()
    {
        _sut.Activities.Should().NotBeNull();
    }

    [Fact]
    public void ActivityParticipants_ShouldReturnDbSet()
    {
        _sut.ActivityParticipants.Should().NotBeNull();
    }

    [Fact]
    public void Expenses_ShouldReturnDbSet()
    {
        _sut.Expenses.Should().NotBeNull();
    }

    [Fact]
    public void ExpenseParticipants_ShouldReturnDbSet()
    {
        _sut.ExpenseParticipants.Should().NotBeNull();
    }

    [Fact]
    public void Settlements_ShouldReturnDbSet()
    {
        _sut.Settlements.Should().NotBeNull();
    }

    [Fact]
    public void ApprovalSteps_ShouldReturnDbSet()
    {
        _sut.ApprovalSteps.Should().NotBeNull();
    }

    [Fact]
    public void Categories_ShouldReturnDbSet()
    {
        _sut.Categories.Should().NotBeNull();
    }

    [Fact]
    public void AuditLogs_ShouldReturnDbSet()
    {
        _sut.AuditLogs.Should().NotBeNull();
    }

    [Fact]
    public void RefreshTokens_ShouldReturnDbSet()
    {
        _sut.RefreshTokens.Should().NotBeNull();
    }

    [Fact]
    public void PushSubscriptions_ShouldReturnDbSet()
    {
        _sut.PushSubscriptions.Should().NotBeNull();
    }

    [Fact]
    public void DataProtectionKeys_ShouldReturnDbSet()
    {
        _sut.DataProtectionKeys.Should().NotBeNull();
    }

    [Fact]
    public void OnModelCreating_AppliesConfigurations()
    {
        // The model should have entity types registered via ApplyConfigurationsFromAssembly
        var entityTypes = _sut.Model.GetEntityTypes().Select(e => e.ClrType).ToList();

        entityTypes.Should().Contain(typeof(FamilySplit.Domain.Entities.User));
        entityTypes.Should().Contain(typeof(FamilySplit.Domain.Entities.Family));
        entityTypes.Should().Contain(typeof(FamilySplit.Domain.Entities.Expense));
    }
}
