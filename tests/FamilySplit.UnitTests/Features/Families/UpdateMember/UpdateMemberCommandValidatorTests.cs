using FamilySplit.Features.Families.UpdateMember;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Features.Families.UpdateMember;

public class UpdateMemberCommandValidatorTests
{
    private readonly UpdateMemberCommandValidator _sut = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var cmd = new UpdateMemberCommand("Alice", "alice@example.com", new DateOnly(1990, 1, 1), 1.5m);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyDisplayName_FailsWithRequiredMessage()
    {
        var cmd = new UpdateMemberCommand(string.Empty, null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void DisplayNameOver100Chars_FailsWithLengthMessage()
    {
        var cmd = new UpdateMemberCommand(new string('A', 101), null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name cannot exceed 100 characters.");
    }

    [Fact]
    public void DisplayNameExactly100Chars_Passes()
    {
        var cmd = new UpdateMemberCommand(new string('A', 100), null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void InvalidEmail_FailsWithEmailMessage()
    {
        var cmd = new UpdateMemberCommand("Alice", "bad-email", null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void EmailOver255Chars_FailsWithLengthMessage()
    {
        var longEmail = new string('a', 250) + "@b.com";
        var cmd = new UpdateMemberCommand("Alice", longEmail, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email cannot exceed 255 characters.");
    }

    [Fact]
    public void ValidEmailExactly255Chars_Passes()
    {
        var email = new string('a', 243) + "@example.com";
        var cmd = new UpdateMemberCommand("Alice", email, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void NullEmail_SkipsEmailValidation()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void FutureDateOfBirth_FailsWithFutureMessage()
    {
        var cmd = new UpdateMemberCommand("Alice", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
              .WithErrorMessage("Date of birth cannot be in the future.");
    }

    [Fact]
    public void TodayDateOfBirth_Passes()
    {
        var cmd = new UpdateMemberCommand("Alice", null, DateOnly.FromDateTime(DateTime.UtcNow), null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void NullDateOfBirth_Passes()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void WeightOverrideZero_FailsWithWeightMessage()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, 0m);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void WeightOverrideAbove10_FailsWithWeightMessage()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, 10.01m);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void WeightOverrideNegative_FailsWithWeightMessage()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, -1m);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void WeightOverrideExactly10_Passes()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, 10m);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    [Fact]
    public void WeightOverrideSmallPositive_Passes()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, 0.01m);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    [Fact]
    public void NullWeightOverride_Passes()
    {
        var cmd = new UpdateMemberCommand("Alice", null, null, null);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }
}
