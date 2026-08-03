using FamilySplit.Features.Admin.AddFamilyMember;
using FluentAssertions;

namespace FamilySplit.UnitTests.Features.Admin.AddFamilyMember;

public class AddFamilyMemberCommandValidatorTests
{
    private readonly AddFamilyMemberCommandValidator _sut = new();

    private static AddFamilyMemberCommand Valid(
        string name = "New Member", string? email = null, DateOnly? dob = null, decimal? weight = null) =>
        new(name, email, dob, weight);

    [Fact]
    public void Validate_Valid_Passes()
    {
        _sut.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyName_Fails(string name)
    {
        var result = _sut.Validate(Valid(name: name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Name is required.");
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        var result = _sut.Validate(Valid(email: "not-an-email"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email must be a valid email address.");
    }

    [Fact]
    public void Validate_FutureDateOfBirth_Fails()
    {
        var result = _sut.Validate(Valid(dob: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Date of birth cannot be in the future.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_WeightOutOfRange_Fails(decimal weight)
    {
        var result = _sut.Validate(Valid(weight: weight));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Weight override must be between 0.01 and 10.");
    }
}
