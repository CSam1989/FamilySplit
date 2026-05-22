using FluentValidation;
using FamilySplit.Application.Families.Dtos;

namespace FamilySplit.Application.Families;

public class UpdateFamilyNameValidator : AbstractValidator<UpdateFamilyNameRequest>
{
    public UpdateFamilyNameValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Family name is required.")
            .MaximumLength(100).WithMessage("Family name cannot exceed 100 characters.");
    }
}

public class AddFamilyMemberValidator : AbstractValidator<AddFamilyMemberRequest>
{
    public AddFamilyMemberValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.")
            .When(x => x.Email is not null);

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob is null || dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.WeightOverride)
            .Must(w => w is null || (w.Value > 0m && w.Value <= 10m))
            .WithMessage("Weight override must be between 0.01 and 10.");
    }
}

public class UpdateFamilyMemberValidator : AbstractValidator<UpdateFamilyMemberRequest>
{
    public UpdateFamilyMemberValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.")
            .When(x => x.Email is not null);

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob is null || dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth cannot be in the future.");

        RuleFor(x => x.WeightOverride)
            .Must(w => w is null || (w.Value > 0m && w.Value <= 10m))
            .WithMessage("Weight override must be between 0.01 and 10.");
    }
}
