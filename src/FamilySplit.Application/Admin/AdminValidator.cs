using FluentValidation;
using FamilySplit.Application.Admin.Dtos;

namespace FamilySplit.Application.Admin;

public class CreateFamilyValidator : AbstractValidator<CreateFamilyRequest>
{
    public CreateFamilyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Family name is required.")
            .MaximumLength(100).WithMessage("Family name cannot exceed 100 characters.");
    }
}
