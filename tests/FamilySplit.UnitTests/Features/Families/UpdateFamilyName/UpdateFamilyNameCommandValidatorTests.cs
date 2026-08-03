using FamilySplit.Features.Families.UpdateFamilyName;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Features.Families.UpdateFamilyName;

public class UpdateFamilyNameCommandValidatorTests
{
    private readonly UpdateFamilyNameCommandValidator _sut = new();

    [Fact]
    public void ValidName_PassesValidation()
    {
        var result = _sut.TestValidate(new UpdateFamilyNameCommand("Smith Family"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_FailsWithRequiredMessage()
    {
        var result = _sut.TestValidate(new UpdateFamilyNameCommand(string.Empty));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Family name is required.");
    }

    [Fact]
    public void NameExactly100Chars_Passes()
    {
        var result = _sut.TestValidate(new UpdateFamilyNameCommand(new string('A', 100)));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NameOver100Chars_FailsWithLengthMessage()
    {
        var result = _sut.TestValidate(new UpdateFamilyNameCommand(new string('A', 101)));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Family name cannot exceed 100 characters.");
    }
}
