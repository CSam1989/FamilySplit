using FluentValidation;

namespace FamilySplit.Features.Activities.Create;

/// <summary>
/// Validates <see cref="CreateActivityCommand"/> — used by both Create and CreateSubActivity, which
/// share the request shape.
/// </summary>
public sealed class CreateActivityCommandValidator : AbstractValidator<CreateActivityCommand>
{
    public CreateActivityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Activity name is required.")
            .MaximumLength(100).WithMessage("Activity name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description cannot exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}
