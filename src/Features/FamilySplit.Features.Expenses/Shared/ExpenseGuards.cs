using FamilySplit.Common.Exceptions;

namespace FamilySplit.Features.Expenses.Shared;

/// <summary>
/// Pure expense-slice business guards shared by the Create, Update, and Delete commands.
/// They operate on plain values fetched via <c>IExpenseData</c> (ADR-001) — no database access —
/// so the command handlers stay EF-free and unit-testable, and these guards are pure unit tests.
/// </summary>
internal static class ExpenseGuards
{
    /// <summary>
    /// Only a member of the family that posted the expense (the payer's family) may modify or
    /// delete it. Global admins bypass this check.
    /// </summary>
    public static void RequireSameFamilyAsPayerOrGlobalAdmin(bool isGlobalAdmin, Guid? callerFamilyId, Guid? payerFamilyId)
    {
        if (isGlobalAdmin)
            return;

        if (callerFamilyId is null || payerFamilyId is null || callerFamilyId.Value != payerFamilyId.Value)
            throw new ForbiddenException("Only a member of the family that posted this expense can modify it.");
    }

    /// <summary>
    /// Settlement balances are computed by summing amounts without FX conversion, so every expense
    /// on an activity must share one currency. Reject a mismatch up front.
    /// </summary>
    public static void EnsureCurrencyConsistent(string? existingCurrency, string currency)
    {
        if (existingCurrency is not null && !string.Equals(existingCurrency, currency, StringComparison.OrdinalIgnoreCase))
            throw ValidationErrors.Field("Currency",
                $"All expenses on an activity must use the same currency ({existingCurrency}).");
    }

    /// <summary>
    /// A category must exist and be either system-wide or scoped to the activity's group.
    /// <paramref name="exists"/> is the result of that lookup via <c>IExpenseData</c>.
    /// </summary>
    public static void EnsureCategoryValid(bool exists)
    {
        if (!exists)
            throw ValidationErrors.Field("CategoryId", "Category not found or not available in this group.");
    }
}
