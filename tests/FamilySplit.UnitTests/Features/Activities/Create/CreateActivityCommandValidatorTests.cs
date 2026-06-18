using FamilySplit.Features.Activities.Create;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Features.Activities.Create;

public class CreateActivityCommandValidatorTests
{
    private readonly CreateActivityCommandValidator _sut = new();

    [Fact]
    public void EmptyName_HasValidationError()
    {
        var result = _sut.TestValidate(new CreateActivityCommand(string.Empty, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name is required.");
    }

    [Fact]
    public void NameTooLong_HasValidationError()
    {
        var result = _sut.TestValidate(new CreateActivityCommand(new string('a', 101), null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name cannot exceed 100 characters.");
    }

    [Fact]
    public void DescriptionTooLong_HasValidationError()
    {
        var result = _sut.TestValidate(new CreateActivityCommand("Valid Name", new string('a', 501)));
        result.ShouldHaveValidationErrorFor(x => x.Description)
              .WithErrorMessage("Description cannot exceed 500 characters.");
    }

    [Fact]
    public void NullDescription_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(new CreateActivityCommand("Valid Name", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ValidRequest_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(new CreateActivityCommand("Valid Name", "A short description."));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NameExactly100Chars_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(new CreateActivityCommand(new string('a', 100), null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DescriptionExactly500Chars_HasNoValidationErrors()
    {
        var result = _sut.TestValidate(new CreateActivityCommand("Valid Name", new string('a', 500)));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
