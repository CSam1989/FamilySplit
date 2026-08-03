using FluentValidation;

namespace FamilySplit.Features.Families.UpdateFamilyName;

public class UpdateFamilyNameCommandValidator : AbstractValidator<UpdateFamilyNameCommand>
{
    public UpdateFamilyNameCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Family name is required.")
            .MaximumLength(100).WithMessage("Family name cannot exceed 100 characters.");
    }
}
