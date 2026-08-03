using FamilySplit.Common.Calculations;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Families.Shared;

namespace FamilySplit.UnitTests.Features.Families.Shared;

public class FamilyMemberMapperTests
{
    [Fact]
    public void ToDto_MapsAllProperties()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            Email = "test@example.com",
            DateOfBirth = new DateOnly(1990, 5, 15),
            WeightOverride = 1.5m,
            IsActive = true,
            IsAdmin = true,
            UserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = FamilyMemberMapper.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));

        result.Id.Should().Be(member.Id);
        result.DisplayName.Should().Be("Test");
        result.Email.Should().Be("test@example.com");
        result.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
        result.WeightOverride.Should().Be(1.5m);
        result.IsActive.Should().BeTrue();
        result.IsAdmin.Should().BeTrue();
        result.IsLinked.Should().BeTrue();
        result.CreatedAt.Should().Be(member.CreatedAt);
    }

    [Fact]
    public void ToDto_NullUserId_IsLinkedFalse()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Unlinked",
            IsActive = true,
            UserId = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = FamilyMemberMapper.ToDto(member, DateOnly.FromDateTime(DateTime.UtcNow));

        result.IsLinked.Should().BeFalse();
    }

    [Fact]
    public void ToDto_NoWeightOverride_UsesCalculatedWeight()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Adult",
            DateOfBirth = new DateOnly(1990, 1, 1),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var asOf = new DateOnly(2024, 6, 1);
        var result = FamilyMemberMapper.ToDto(member, asOf);

        result.CurrentWeight.Should().Be(WeightCalculator.GetWeight(member, asOf));
        result.CurrentTier.Should().Be(WeightCalculator.GetTier(member, asOf));
    }
}
