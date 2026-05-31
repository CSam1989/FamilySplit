using FamilySplit.Application.Core;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;

namespace FamilySplit.UnitTests.Core;

public class WeightCalculatorTests
{
    private static readonly DateOnly ReferenceDate = new(2026, 5, 21);

    [Theory]
    [InlineData(2024, 1, 1, 0.25)]  // age 2  -> kleuterschool
    [InlineData(2020, 5, 21, 0.50)] // age 6  -> lager onderwijs (boundary)
    [InlineData(2015, 5, 22, 0.50)] // age 10 -> lager onderwijs
    [InlineData(2014, 5, 21, 0.75)] // age 12 -> middelbaar onderwijs (boundary)
    [InlineData(2010, 5, 21, 0.75)] // age 16 -> middelbaar onderwijs
    [InlineData(2008, 5, 21, 1.00)] // age 18 -> volwassene (boundary)
    [InlineData(1990, 1, 1, 1.00)]  // adult
    public void GetWeight_returns_tier_for_age(int year, int month, int day, decimal expected)
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            DateOfBirth = new DateOnly(year, month, day)
        };

        WeightCalculator.GetWeight(member, ReferenceDate).Should().Be(expected);
    }

    [Fact]
    public void GetWeight_uses_override_when_set()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Override Eddie",
            DateOfBirth = new DateOnly(2000, 1, 1),
            WeightOverride = 0.10m
        };

        WeightCalculator.GetWeight(member, ReferenceDate).Should().Be(0.10m);
    }

    [Fact]
    public void GetWeight_falls_back_to_adult_when_dob_missing()
    {
        var member = new FamilyMember { Id = Guid.NewGuid(), DisplayName = "Unknown" };

        WeightCalculator.GetWeight(member, ReferenceDate).Should().Be(1.00m);
    }

    [Fact]
    public void GetTier_returns_Override_when_override_set()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Override",
            WeightOverride = 0.5m,
            DateOfBirth = new DateOnly(2000, 1, 1)
        };

        WeightCalculator.GetTier(member, ReferenceDate).Should().Be(WeightTier.Override);
    }

    [Fact]
    public void Birthday_not_yet_reached_this_year_drops_age_by_one()
    {
        // Born May 22 — on May 21 the person hasn't had their birthday yet.
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Just under",
            DateOfBirth = new DateOnly(2008, 5, 22)
        };

        // On ReferenceDate (May 21 2026) they are still 17 → middelbaar onderwijs
        WeightCalculator.GetWeight(member, ReferenceDate).Should().Be(0.75m);
    }

    [Fact]
    public void GetTier_returns_Volwassene_when_dob_missing()
    {
        var member = new FamilyMember { Id = Guid.NewGuid(), DisplayName = "No DOB" };

        WeightCalculator.GetTier(member, ReferenceDate).Should().Be(WeightTier.Volwassene);
    }

    [Theory]
    [InlineData(2024, 1, 1, WeightTier.Kleuterschool)]
    [InlineData(2020, 5, 21, WeightTier.LagerOnderwijs)]
    [InlineData(2014, 5, 21, WeightTier.MiddelbaarOnderwijs)]
    [InlineData(2008, 5, 21, WeightTier.Volwassene)]
    public void GetTier_returns_correct_tier_for_age(int year, int month, int day, WeightTier expected)
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test",
            DateOfBirth = new DateOnly(year, month, day),
        };

        WeightCalculator.GetTier(member, ReferenceDate).Should().Be(expected);
    }

    [Fact]
    public void GetTier_birthday_not_yet_reached_drops_age_by_one()
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            DisplayName = "Just under",
            DateOfBirth = new DateOnly(2008, 5, 22),
        };

        // Still 17 on May 21 → MiddelbaarOnderwijs
        WeightCalculator.GetTier(member, ReferenceDate).Should().Be(WeightTier.MiddelbaarOnderwijs);
    }
}
