using FamilySplit.Application.Families;
using FamilySplit.Application.Families.Dtos;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Families;

public class FamilyValidatorTests
{
    // ── UpdateFamilyNameValidator ────────────────────────────────────────────

    private readonly UpdateFamilyNameValidator _nameValidator = new();

    [Fact]
    public void UpdateFamilyNameValidator_ValidName_PassesValidation()
    {
        var req = new UpdateFamilyNameRequest("Smith Family");
        var result = _nameValidator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateFamilyNameValidator_EmptyName_FailsWithRequiredMessage()
    {
        var req = new UpdateFamilyNameRequest(string.Empty);
        var result = _nameValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Family name is required.");
    }

    [Fact]
    public void UpdateFamilyNameValidator_NameExactly100Chars_Passes()
    {
        var req = new UpdateFamilyNameRequest(new string('A', 100));
        var result = _nameValidator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateFamilyNameValidator_NameOver100Chars_FailsWithLengthMessage()
    {
        var req = new UpdateFamilyNameRequest(new string('A', 101));
        var result = _nameValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Family name cannot exceed 100 characters.");
    }

    // ── AddFamilyMemberValidator ─────────────────────────────────────────────

    private readonly AddFamilyMemberValidator _addValidator = new();

    [Fact]
    public void AddFamilyMemberValidator_ValidRequest_PassesValidation()
    {
        var req = new AddFamilyMemberRequest("Alice", "alice@example.com", new DateOnly(1990, 1, 1), 1.5m);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddFamilyMemberValidator_EmptyDisplayName_FailsWithRequiredMessage()
    {
        var req = new AddFamilyMemberRequest(string.Empty, null, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void AddFamilyMemberValidator_DisplayNameOver100Chars_FailsWithLengthMessage()
    {
        var req = new AddFamilyMemberRequest(new string('A', 101), null, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name cannot exceed 100 characters.");
    }

    [Fact]
    public void AddFamilyMemberValidator_InvalidEmail_FailsWithEmailMessage()
    {
        var req = new AddFamilyMemberRequest("Alice", "not-an-email", null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void AddFamilyMemberValidator_EmailOver255Chars_FailsWithLengthMessage()
    {
        var longEmail = new string('a', 250) + "@b.com";
        var req = new AddFamilyMemberRequest("Alice", longEmail, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email cannot exceed 255 characters.");
    }

    [Fact]
    public void AddFamilyMemberValidator_NullEmail_SkipsEmailValidation()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void AddFamilyMemberValidator_FutureDateOfBirth_FailsWithFutureMessage()
    {
        var req = new AddFamilyMemberRequest("Alice", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
              .WithErrorMessage("Date of birth cannot be in the future.");
    }

    [Fact]
    public void AddFamilyMemberValidator_TodayDateOfBirth_Passes()
    {
        var req = new AddFamilyMemberRequest("Alice", null, DateOnly.FromDateTime(DateTime.UtcNow), null);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void AddFamilyMemberValidator_NullDateOfBirth_Passes()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void AddFamilyMemberValidator_WeightOverrideZero_FailsWithWeightMessage()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, 0m);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void AddFamilyMemberValidator_WeightOverrideAbove10_FailsWithWeightMessage()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, 10.01m);
        var result = _addValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void AddFamilyMemberValidator_WeightOverrideExactly10_Passes()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, 10m);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    [Fact]
    public void AddFamilyMemberValidator_WeightOverrideSmallPositive_Passes()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, 0.01m);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    [Fact]
    public void AddFamilyMemberValidator_NullWeightOverride_Passes()
    {
        var req = new AddFamilyMemberRequest("Alice", null, null, null);
        var result = _addValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    // ── UpdateFamilyMemberValidator ──────────────────────────────────────────

    private readonly UpdateFamilyMemberValidator _updateValidator = new();

    [Fact]
    public void UpdateFamilyMemberValidator_ValidRequest_PassesValidation()
    {
        var req = new UpdateFamilyMemberRequest("Alice", "alice@example.com", new DateOnly(1990, 1, 1), 1.5m);
        var result = _updateValidator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateFamilyMemberValidator_EmptyDisplayName_FailsWithRequiredMessage()
    {
        var req = new UpdateFamilyMemberRequest(string.Empty, null, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name is required.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_DisplayNameOver100Chars_FailsWithLengthMessage()
    {
        var req = new UpdateFamilyMemberRequest(new string('A', 101), null, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
              .WithErrorMessage("Name cannot exceed 100 characters.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_InvalidEmail_FailsWithEmailMessage()
    {
        var req = new UpdateFamilyMemberRequest("Alice", "bad-email", null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email must be a valid email address.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_EmailOver255Chars_FailsWithLengthMessage()
    {
        var longEmail = new string('a', 250) + "@b.com";
        var req = new UpdateFamilyMemberRequest("Alice", longEmail, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Email cannot exceed 255 characters.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_NullEmail_SkipsEmailValidation()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void UpdateFamilyMemberValidator_FutureDateOfBirth_FailsWithFutureMessage()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth)
              .WithErrorMessage("Date of birth cannot be in the future.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_NullDateOfBirth_Passes()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void UpdateFamilyMemberValidator_WeightOverrideZero_FailsWithWeightMessage()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, 0m);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_WeightOverrideAbove10_FailsWithWeightMessage()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, 10.01m);
        var result = _updateValidator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.WeightOverride)
              .WithErrorMessage("Weight override must be between 0.01 and 10.");
    }

    [Fact]
    public void UpdateFamilyMemberValidator_WeightOverrideExactly10_Passes()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, 10m);
        var result = _updateValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }

    [Fact]
    public void UpdateFamilyMemberValidator_NullWeightOverride_Passes()
    {
        var req = new UpdateFamilyMemberRequest("Alice", null, null, null);
        var result = _updateValidator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(x => x.WeightOverride);
    }
}
