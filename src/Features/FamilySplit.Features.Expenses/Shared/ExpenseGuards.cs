using FamilySplit.Common.Exceptions;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Features.Expenses.Shared;

/// <summary>
/// Expense-slice business guards shared by the Create, Update, and Delete commands.
/// Static methods that take <see cref="AppDbContext"/> as a parameter (rather than a
/// ctor dependency) so they remain pure helpers consumed by the command handlers.
/// </summary>
internal static class ExpenseGuards
{
    /// <summary>
    /// Only a member of the family that posted the expense (the payer's family) may
    /// modify or delete it. Global admins bypass this check. The payer lookup is not
    /// filtered by IsActive — the family that fronted the money is the same regardless
    /// of whether that member has since been deactivated.
    /// </summary>
    public static async Task RequireSameFamilyAsPayerOrGlobalAdminAsync(
        AppDbContext db, Guid payerUserId, Guid callerId, CancellationToken ct)
    {
        var isGlobalAdmin = await db.Users
            .Where(u => u.Id == callerId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync(ct);

        if (isGlobalAdmin)
            return;

        var callerFamilyId = await db.FamilyMembers
            .Where(m => m.UserId == callerId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        var payerFamilyId = await db.FamilyMembers
            .Where(m => m.UserId == payerUserId)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (callerFamilyId is null || payerFamilyId is null || callerFamilyId.Value != payerFamilyId.Value)
            throw new ForbiddenException("Only a member of the family that posted this expense can modify it.");
    }

    /// <summary>
    /// Settlement balances are computed by summing amounts without FX conversion, so
    /// every expense on an activity must share one currency. Reject a mismatch up front.
    /// </summary>
    public static async Task EnsureCurrencyConsistentAsync(
        AppDbContext db, Guid activityId, string currency, Guid? excludeExpenseId, CancellationToken ct)
    {
        var existingCurrency = await db.Expenses
            .Where(e => e.ActivityId == activityId && (excludeExpenseId == null || e.Id != excludeExpenseId))
            .Select(e => e.Currency)
            .FirstOrDefaultAsync(ct);

        if (existingCurrency is not null && !string.Equals(existingCurrency, currency, StringComparison.OrdinalIgnoreCase))
            throw ValidationErrors.Field("Currency",
                $"All expenses on an activity must use the same currency ({existingCurrency}).");
    }

    /// <summary>
    /// A category must exist and be either system-wide or scoped to the activity's group.
    /// Prevents attaching another group's custom category and turns a bad id into a 422
    /// instead of an unhandled FK violation.
    /// </summary>
    public static async Task EnsureCategoryValidAsync(
        AppDbContext db, Guid? categoryId, Guid groupId, CancellationToken ct)
    {
        if (categoryId is null)
            return;

        var ok = await db.Categories
            .AnyAsync(c => c.Id == categoryId.Value && (c.GroupId == null || c.GroupId == groupId), ct);

        if (!ok)
            throw ValidationErrors.Field("CategoryId", "Category not found or not available in this group.");
    }
}
