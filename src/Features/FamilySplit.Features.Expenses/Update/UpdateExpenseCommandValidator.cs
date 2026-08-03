using FamilySplit.Features.Expenses.Shared;
using FluentValidation;

namespace FamilySplit.Features.Expenses.Update;

public class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must be at most 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be at most 500 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.TotalAmount).ApplyAmountRules();

        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be a 3-letter ISO code.")
            .When(x => x.Currency is not null);

        RuleFor(x => x.ExpenseDate)
            .NotEmpty().WithMessage("Expense date is required.");
    }
}
