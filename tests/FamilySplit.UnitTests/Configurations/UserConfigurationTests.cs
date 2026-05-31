using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure.Configurations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace FamilySplit.UnitTests.Configurations;

public class UserConfigurationTests
{
    private static IEntityType BuildEntityType()
    {
        var conventionSet = new ConventionSet();
        var modelBuilder = new ModelBuilder(conventionSet);
        var config = new UserConfiguration();
        config.Configure(modelBuilder.Entity<User>());
        var model = modelBuilder.FinalizeModel();
        return model.FindEntityType(typeof(User))!;
    }

    [Fact]
    public void Configure_SetsTableName_ToUsers()
    {
        var entityType = BuildEntityType();
        entityType.GetTableName().Should().Be("users");
    }

    [Fact]
    public void Configure_SetsPrimaryKey_ToId()
    {
        var entityType = BuildEntityType();
        entityType.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("Id");
    }

    [Fact]
    public void Configure_MapsColumnNames_Correctly()
    {
        var entityType = BuildEntityType();

        entityType.FindProperty("Id")!.GetColumnName().Should().Be("id");
        entityType.FindProperty("ExternalId")!.GetColumnName().Should().Be("external_id");
        entityType.FindProperty("Provider")!.GetColumnName().Should().Be("provider");
        entityType.FindProperty("Email")!.GetColumnName().Should().Be("email");
        entityType.FindProperty("DisplayName")!.GetColumnName().Should().Be("display_name");
        entityType.FindProperty("AvatarUrl")!.GetColumnName().Should().Be("avatar_url");
        entityType.FindProperty("IsGlobalAdmin")!.GetColumnName().Should().Be("is_global_admin");
        entityType.FindProperty("CreatedAt")!.GetColumnName().Should().Be("created_at");
    }

    [Fact]
    public void Configure_ExternalId_HasMaxLength255AndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("ExternalId")!;
        prop.GetMaxLength().Should().Be(255);
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_Provider_HasMaxLength20AndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("Provider")!;
        prop.GetMaxLength().Should().Be(20);
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_Email_HasMaxLength255AndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("Email")!;
        prop.GetMaxLength().Should().Be(255);
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_DisplayName_HasMaxLength100AndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("DisplayName")!;
        prop.GetMaxLength().Should().Be(100);
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_AvatarUrl_HasMaxLength500AndIsOptional()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("AvatarUrl")!;
        prop.GetMaxLength().Should().Be(500);
        prop.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void Configure_IsGlobalAdmin_HasDefaultValueFalseAndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("IsGlobalAdmin")!;
        prop.GetDefaultValue().Should().Be(false);
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_CreatedAt_HasDefaultValueSqlAndIsRequired()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("CreatedAt")!;
        prop.GetDefaultValueSql().Should().Be("now()");
        prop.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void Configure_EmailIndex_IsUnique()
    {
        var entityType = BuildEntityType();
        var index = entityType.GetIndexes().First(i => i.Properties.Any(p => p.Name == "Email"));
        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Configure_ProviderExternalIdIndex_IsUnique()
    {
        var entityType = BuildEntityType();
        var index = entityType.GetIndexes().First(i => i.Properties.Count == 2 && i.Properties.Any(p => p.Name == "Provider") && i.Properties.Any(p => p.Name == "ExternalId"));
        index.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Configure_Provider_StoredAsString()
    {
        var entityType = BuildEntityType();
        var prop = entityType.FindProperty("Provider")!;
        prop.GetProviderClrType().Should().Be(typeof(string));
    }
}
