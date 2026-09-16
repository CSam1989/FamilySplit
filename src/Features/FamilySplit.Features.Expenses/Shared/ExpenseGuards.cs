using FamilySplit.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.Shared;

/// <summary>
/// Pure expense-slice business guards shared by the Create, Update, and Delete commands.
/// They operate on plain values fetched via <c>IExpenseData</c> (ADR-001) — no database access —
/// so the command handlers stay EF-free and unit-testable, and these guards are pure unit tests.
/// Each guard takes the caller's <see cref="ILogger"/> so the rejection path can be logged
/// (Guard/condition logging) right where it throws.
/// </summary>
internal static class ExpenseGuards
{
    /// <summary>
    /// Only a member of the family that posted the expense (the payer's family) may modify or
    /// delete it. Global admins bypass this check.
    /// </summary>
    public static void RequireSameFamilyAsPayerOrGlobalAdmin(
        bool isGlobalAdmin, Guid? callerFamilyId, Guid? payerFamilyId, Guid expenseId, Guid callerId, ILogger logger)
    {
        if (isGlobalAdmin)
            return;

        if (callerFamilyId is null || payerFamilyId is null || callerFamilyId.Value != payerFamilyId.Value)
        {
            logger.LogWarning(
                "User {UserId} outside the payer's family attempted to modify expense {ExpenseId}", callerId, expenseId);
            throw new ForbiddenException("Only a member of the family that posted this expense can modify it.");
        }
    }

    /// <summary>
    /// Settlement balances are computed by summing amounts without FX conversion, so every expense
    /// on an activity must share one currency. Reject a mismatch up front.
    /// </summary>
    public static void EnsureCurrencyConsistent(string? existingCurrency, string currency, Guid activityId, ILogger logger)
    {
        if (existingCurrency is not null && !string.Equals(existingCurrency, currency, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Currency mismatch on activity {ActivityId}", activityId);
            throw ValidationErrors.Field("Currency",
                $"All expenses on an activity must use the same currency ({existingCurrency}).");
        }
    }

    /// <summary>
    /// A category must exist and be either system-wide or scoped to the activity's group.
    /// <paramref name="exists"/> is the result of that lookup via <c>IExpenseData</c>.
    /// </summary>
    public static void EnsureCategoryValid(bool exists, Guid categoryId, ILogger logger)
    {
        if (!exists)
        {
            logger.LogDebug("Category {CategoryId} not valid for group", categoryId);
            throw ValidationErrors.Field("CategoryId", "Category not found or not available in this group.");
        }
    }
}
