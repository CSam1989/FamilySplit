using FamilySplit.Application.Expenses.Dtos;
using FluentValidation;

namespace FamilySplit.Application.Expenses;

internal static class ExpenseAmountRules
{
    // Amounts are persisted as numeric(12,2); cap well below overflow and forbid
    // sub-cent values that Postgres would silently round (breaking the share sum).
    public const decimal MaxAmount = 1_000_000m;

    public static IRuleBuilderOptions<T, decimal> ApplyAmountRules<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(MaxAmount).WithMessage("Amount cannot exceed 1,000,000.")
            .Must(a => decimal.Round(a, 2) == a)
            .WithMessage("Amount cannot have more than 2 decimal places.");
}

public class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator()
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

public class UpdateExpenseValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseValidator()
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
