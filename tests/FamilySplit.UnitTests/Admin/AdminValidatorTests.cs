using FamilySplit.Application.Admin;
using FamilySplit.Application.Admin.Dtos;
using FluentAssertions;

namespace FamilySplit.UnitTests.Admin;

public class AdminValidatorTests
{
    private readonly CreateFamilyValidator _sut = new();

    [Fact]
    public void Validate_ValidName_Passes()
    {
        var result = _sut.Validate(new CreateFamilyRequest("Smith Family"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceName_FailsWithRequiredMessage(string name)
    {
        var result = _sut.Validate(new CreateFamilyRequest(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Family name is required.");
    }

    [Fact]
    public void Validate_NameExactly100Characters_Passes()
    {
        var name = new string('a', 100);

        var result = _sut.Validate(new CreateFamilyRequest(name));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NameExceeds100Characters_FailsWithMaxLengthMessage()
    {
        var name = new string('a', 101);

        var result = _sut.Validate(new CreateFamilyRequest(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Family name cannot exceed 100 characters.");
    }
}
