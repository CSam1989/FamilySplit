using FamilySplit.Features.Groups.Join;

namespace FamilySplit.UnitTests.Features.Groups.Join;

public class JoinGroupCommandValidatorTests
{
    private readonly JoinGroupCommandValidator _sut = new();

    [Fact]
    public void InviteCodeExactly8Chars_PassesValidation()
    {
        var result = _sut.Validate(new JoinGroupCommand("ABCD1234"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyInviteCode_FailsWithMessage()
    {
        var result = _sut.Validate(new JoinGroupCommand(string.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InviteCode" && e.ErrorMessage == "Invite code is required.");
    }

    [Fact]
    public void NullInviteCode_FailsWithMessage()
    {
        var result = _sut.Validate(new JoinGroupCommand(null!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InviteCode" && e.ErrorMessage == "Invite code is required.");
    }

    [Fact]
    public void InviteCodeTooShort_FailsWithMessage()
    {
        var result = _sut.Validate(new JoinGroupCommand("SHORT"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InviteCode" && e.ErrorMessage == "Invite code must be exactly 8 characters.");
    }

    [Fact]
    public void InviteCodeTooLong_FailsWithMessage()
    {
        var result = _sut.Validate(new JoinGroupCommand("TOOLONGCODE"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "InviteCode" && e.ErrorMessage == "Invite code must be exactly 8 characters.");
    }
}
