using FluentValidation;

namespace FamilySplit.Features.Admin.CreateFamily;

public class CreateFamilyCommandValidator : AbstractValidator<CreateFamilyCommand>
{
    public CreateFamilyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Family name is required.")
            .MaximumLength(100).WithMessage("Family name cannot exceed 100 characters.");
    }
}
