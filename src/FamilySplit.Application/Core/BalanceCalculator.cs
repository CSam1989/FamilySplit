namespace FamilySplit.Application.Core;

/// <summary>
/// Computes the net balance per Family from a flat list of expense data.
/// Positive balance  = the family is owed money (creditor).
/// Negative balance  = the family owes money (debtor).
/// </summary>
public static class BalanceCalculator
{
    public readonly record struct ExpenseData(
        Guid PayerFamilyId,
        decimal TotalAmount);

    public readonly record struct ParticipantData(
        Guid FamilyId,
        decimal CalculatedAmount);

    /// <summary>
    /// Calculates net balances from a set of expenses and their participants.
    /// </summary>
    /// <param name="expenses">One entry per expense: the payer's family and the total amount paid.</param>
    /// <param name="participants">All expense-participant rows across all expenses: family and their calculated share.</param>
    /// <returns>Dictionary keyed by FamilyId; value is the net balance (positive = creditor, negative = debtor).</returns>
    public static Dictionary<Guid, decimal> Compute(
        IEnumerable<ExpenseData> expenses,
        IEnumerable<ParticipantData> participants)
    {
        var balances = new Dictionary<Guid, decimal>();

        // Payer family fronted the TotalAmount → credit them.
        foreach (var e in expenses)
        {
            balances.TryGetValue(e.PayerFamilyId, out var cur);
            balances[e.PayerFamilyId] = cur + e.TotalAmount;
        }

        // Each participant owes their CalculatedAmount → debit their family.
        foreach (var p in participants)
        {
            balances.TryGetValue(p.FamilyId, out var cur);
            balances[p.FamilyId] = cur - p.CalculatedAmount;
        }

        return balances;
    }
}
