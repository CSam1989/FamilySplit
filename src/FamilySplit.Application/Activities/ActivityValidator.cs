using FamilySplit.Application.Activities.Dtos;
using FluentValidation;

namespace FamilySplit.Application.Activities;

public class CreateActivityValidator : AbstractValidator<CreateActivityRequest>
{
    public CreateActivityValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Activity name is required.")
            .MaximumLength(100).WithMessage("Activity name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

public class UpdateActivityValidator : AbstractValidator<UpdateActivityRequest>
{
    public UpdateActivityValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Activity name is required.")
            .MaximumLength(100).WithMessage("Activity name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}
