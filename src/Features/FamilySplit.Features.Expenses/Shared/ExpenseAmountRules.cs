using FluentValidation;

namespace FamilySplit.Features.Expenses.Shared;

/// <summary>
/// Amount validation rules shared by the Create and Update command validators.
/// </summary>
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
