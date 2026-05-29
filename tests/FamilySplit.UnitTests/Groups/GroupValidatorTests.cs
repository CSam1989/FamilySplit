using FamilySplit.Application.Groups;
using FamilySplit.Application.Groups.Dtos;
using FluentAssertions;

namespace FamilySplit.UnitTests.Groups;

public class GroupValidatorTests
{
    // CreateGroupValidator

    [Fact]
    public void CreateGroupValidator_EmptyName_ShouldFail()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest(string.Empty, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void CreateGroupValidator_NullName_ShouldFail()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest(null!, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void CreateGroupValidator_NameExceeds100_ShouldFail()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest(new string('a', 101), null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name cannot exceed 100 characters.");
    }

    [Fact]
    public void CreateGroupValidator_NameExactly100_ShouldPass()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest(new string('a', 100), null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateGroupValidator_NullDescription_ShouldPass()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest("Valid Name", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateGroupValidator_DescriptionExceeds500_ShouldFail()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest("Valid Name", new string('a', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Description cannot exceed 500 characters.");
    }

    [Fact]
    public void CreateGroupValidator_DescriptionExactly500_ShouldPass()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest("Valid Name", new string('a', 500)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateGroupValidator_ValidNameAndDescription_ShouldPass()
    {
        var validator = new CreateGroupValidator();
        var result = validator.Validate(new CreateGroupRequest("My Group", "A description"));
        result.IsValid.Should().BeTrue();
    }

    // UpdateGroupValidator

    [Fact]
    public void UpdateGroupValidator_EmptyName_ShouldFail()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest(string.Empty, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void UpdateGroupValidator_NullName_ShouldFail()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest(null!, null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name is required.");
    }

    [Fact]
    public void UpdateGroupValidator_NameExceeds100_ShouldFail()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest(new string('a', 101), null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Group name cannot exceed 100 characters.");
    }

    [Fact]
    public void UpdateGroupValidator_NameExactly100_ShouldPass()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest(new string('a', 100), null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateGroupValidator_NullDescription_ShouldPass()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest("Valid Name", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateGroupValidator_DescriptionExceeds500_ShouldFail()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest("Valid Name", new string('a', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Description cannot exceed 500 characters.");
    }

    [Fact]
    public void UpdateGroupValidator_DescriptionExactly500_ShouldPass()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest("Valid Name", new string('a', 500)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateGroupValidator_ValidNameAndDescription_ShouldPass()
    {
        var validator = new UpdateGroupValidator();
        var result = validator.Validate(new UpdateGroupRequest("My Group", "A description"));
        result.IsValid.Should().BeTrue();
    }

    // JoinGroupValidator

    [Fact]
    public void JoinGroupValidator_EmptyInviteCode_ShouldFail()
    {
        var validator = new JoinGroupValidator();
        var result = validator.Validate(new JoinGroupRequest(string.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Invite code is required.");
    }

    [Fact]
    public void JoinGroupValidator_NullInviteCode_ShouldFail()
    {
        var validator = new JoinGroupValidator();
        var result = validator.Validate(new JoinGroupRequest(null!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Invite code is required.");
    }

    [Fact]
    public void JoinGroupValidator_InviteCodeNot8Chars_ShouldFail()
    {
        var validator = new JoinGroupValidator();
        var result = validator.Validate(new JoinGroupRequest("SHORT"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Invite code must be exactly 8 characters.");
    }

    [Fact]
    public void JoinGroupValidator_InviteCodeExceeds8Chars_ShouldFail()
    {
        var validator = new JoinGroupValidator();
        var result = validator.Validate(new JoinGroupRequest("TOOLONGCODE"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Invite code must be exactly 8 characters.");
    }

    [Fact]
    public void JoinGroupValidator_InviteCodeExactly8Chars_ShouldPass()
    {
        var validator = new JoinGroupValidator();
        var result = validator.Validate(new JoinGroupRequest("ABCD1234"));
        result.IsValid.Should().BeTrue();
    }
}
