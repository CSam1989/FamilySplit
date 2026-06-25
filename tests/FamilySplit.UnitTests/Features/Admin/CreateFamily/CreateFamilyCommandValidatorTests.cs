using FamilySplit.Features.Admin.CreateFamily;
using FluentAssertions;

namespace FamilySplit.UnitTests.Features.Admin.CreateFamily;

public class CreateFamilyCommandValidatorTests
{
    private readonly CreateFamilyCommandValidator _sut = new();

    [Fact]
    public void Validate_ValidName_Passes()
    {
        var result = _sut.Validate(new CreateFamilyCommand("Smith Family"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyOrWhitespaceName_FailsWithRequiredMessage(string name)
    {
        var result = _sut.Validate(new CreateFamilyCommand(name));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Family name is required.");
    }

    [Fact]
    public void Validate_NameExactly100Characters_Passes()
    {
        var result = _sut.Validate(new CreateFamilyCommand(new string('a', 100)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NameExceeds100Characters_FailsWithMaxLengthMessage()
    {
        var result = _sut.Validate(new CreateFamilyCommand(new string('a', 101)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage == "Family name cannot exceed 100 characters.");
    }
}
