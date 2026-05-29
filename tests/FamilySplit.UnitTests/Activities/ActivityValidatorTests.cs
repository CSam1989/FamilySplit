using FamilySplit.Application.Activities;
using FamilySplit.Application.Activities.Dtos;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace FamilySplit.UnitTests.Activities;

public class ActivityValidatorTests
{
    // ── AddParticipantValidator ──────────────────────────────────────────────

    [Fact]
    public void AddParticipantValidator_EmptyFamilyMemberId_HasValidationError()
    {
        var validator = new AddParticipantValidator();
        var result = validator.TestValidate(new AddParticipantRequest(Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.FamilyMemberId)
              .WithErrorMessage("FamilyMemberId is required.");
    }

    [Fact]
    public void AddParticipantValidator_ValidFamilyMemberId_HasNoValidationErrors()
    {
        var validator = new AddParticipantValidator();
        var result = validator.TestValidate(new AddParticipantRequest(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── CreateActivityValidator ──────────────────────────────────────────────

    [Fact]
    public void CreateActivityValidator_EmptyName_HasValidationError()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest(string.Empty, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name is required.");
    }

    [Fact]
    public void CreateActivityValidator_NameTooLong_HasValidationError()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest(new string('a', 101), null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name cannot exceed 100 characters.");
    }

    [Fact]
    public void CreateActivityValidator_DescriptionTooLong_HasValidationError()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest("Valid Name", new string('a', 501)));
        result.ShouldHaveValidationErrorFor(x => x.Description)
              .WithErrorMessage("Description cannot exceed 500 characters.");
    }

    [Fact]
    public void CreateActivityValidator_NullDescription_HasNoValidationErrors()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest("Valid Name", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateActivityValidator_ValidRequest_HasNoValidationErrors()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest("Valid Name", "A short description."));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateActivityValidator_NameExactly100Chars_HasNoValidationErrors()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest(new string('a', 100), null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateActivityValidator_DescriptionExactly500Chars_HasNoValidationErrors()
    {
        var validator = new CreateActivityValidator();
        var result = validator.TestValidate(new CreateActivityRequest("Valid Name", new string('a', 500)));
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── UpdateActivityValidator ──────────────────────────────────────────────

    [Fact]
    public void UpdateActivityValidator_EmptyName_HasValidationError()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest(string.Empty, null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name is required.");
    }

    [Fact]
    public void UpdateActivityValidator_NameTooLong_HasValidationError()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest(new string('a', 101), null));
        result.ShouldHaveValidationErrorFor(x => x.Name)
              .WithErrorMessage("Activity name cannot exceed 100 characters.");
    }

    [Fact]
    public void UpdateActivityValidator_DescriptionTooLong_HasValidationError()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest("Valid Name", new string('a', 501)));
        result.ShouldHaveValidationErrorFor(x => x.Description)
              .WithErrorMessage("Description cannot exceed 500 characters.");
    }

    [Fact]
    public void UpdateActivityValidator_NullDescription_HasNoValidationErrors()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest("Valid Name", null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateActivityValidator_ValidRequest_HasNoValidationErrors()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest("Valid Name", "A short description."));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateActivityValidator_NameExactly100Chars_HasNoValidationErrors()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest(new string('a', 100), null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateActivityValidator_DescriptionExactly500Chars_HasNoValidationErrors()
    {
        var validator = new UpdateActivityValidator();
        var result = validator.TestValidate(new UpdateActivityRequest("Valid Name", new string('a', 500)));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
