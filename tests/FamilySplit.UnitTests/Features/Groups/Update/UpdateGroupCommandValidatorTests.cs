using FamilySplit.Features.Groups.Update;

namespace FamilySplit.UnitTests.Features.Groups.Update;

public class UpdateGroupCommandValidatorTests
{
    private readonly UpdateGroupCommandValidator _sut = new();

    [Fact]
    public void ValidNameAndDescription_PassesValidation()
    {
        var result = _sut.Validate(new UpdateGroupCommand("My Group", "A description"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyName_FailsWithMessage()
    {
        var result = _sut.Validate(new UpdateGroupCommand(string.Empty, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void NullName_FailsWithMessage()
    {
        var result = _sut.Validate(new UpdateGroupCommand(null!, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void NameExceeds100_FailsWithMessage()
    {
        var result = _sut.Validate(new UpdateGroupCommand(new string('a', 101), null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage == "Group name cannot exceed 100 characters.");
    }

    [Fact]
    public void NameExactly100_PassesValidation()
    {
        var result = _sut.Validate(new UpdateGroupCommand(new string('a', 100), null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullDescription_PassesValidation()
    {
        var result = _sut.Validate(new UpdateGroupCommand("Valid Name", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DescriptionExceeds500_FailsWithMessage()
    {
        var result = _sut.Validate(new UpdateGroupCommand("Valid Name", new string('a', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage == "Description cannot exceed 500 characters.");
    }

    [Fact]
    public void DescriptionExactly500_PassesValidation()
    {
        var result = _sut.Validate(new UpdateGroupCommand("Valid Name", new string('a', 500)));
        result.IsValid.Should().BeTrue();
    }
}
