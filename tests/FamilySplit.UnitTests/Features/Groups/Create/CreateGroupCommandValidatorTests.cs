using FamilySplit.Features.Groups.Create;

namespace FamilySplit.UnitTests.Features.Groups.Create;

public class CreateGroupCommandValidatorTests
{
    private readonly CreateGroupCommandValidator _sut = new();

    [Fact]
    public void ValidNameAndDescription_PassesValidation()
    {
        var result = _sut.Validate(new CreateGroupCommand("My Group", "A description"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyName_FailsWithMessage()
    {
        var result = _sut.Validate(new CreateGroupCommand(string.Empty, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void NullName_FailsWithMessage()
    {
        var result = _sut.Validate(new CreateGroupCommand(null!, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void NameExceeds100_FailsWithMessage()
    {
        var result = _sut.Validate(new CreateGroupCommand(new string('a', 101), null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name cannot exceed 100 characters.");
    }

    [Fact]
    public void NameExactly100_PassesValidation()
    {
        var result = _sut.Validate(new CreateGroupCommand(new string('a', 100), null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullDescription_PassesValidation()
    {
        var result = _sut.Validate(new CreateGroupCommand("Valid Name", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DescriptionExceeds500_FailsWithMessage()
    {
        var result = _sut.Validate(new CreateGroupCommand("Valid Name", new string('a', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage == "Description cannot exceed 500 characters.");
    }

    [Fact]
    public void DescriptionExactly500_PassesValidation()
    {
        var result = _sut.Validate(new CreateGroupCommand("Valid Name", new string('a', 500)));
        result.IsValid.Should().BeTrue();
    }
}
