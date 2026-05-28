namespace FamilySplit.Application.Core;

/// <summary>
/// Greedy minimum-transfer settlement optimiser.
/// Produces at most N-1 transfers for N families by matching the largest
/// debtor with the largest creditor on each step.
/// </summary>
public static class SettlementOptimiser
{
    public readonly record struct Transfer(
        Guid PayerFamilyId,
        Guid ReceiverFamilyId,
        decimal Amount);

    private const decimal Epsilon = 0.005m; // ignore sub-cent rounding residuals

    /// <summary>
    /// Produces the minimum set of money transfers that settle all balances.
    /// </summary>
    /// <param name="balances">Net balance per family (positive = creditor, negative = debtor).</param>
    /// <returns>List of transfers; at most N-1 entries for N families.</returns>
    public static List<Transfer> Optimise(Dictionary<Guid, decimal> balances)
    {
        // Work on mutable copies, ignoring near-zero residuals.
        var debtors = balances
            .Where(kv => kv.Value < -Epsilon)
            .Select(kv => (FamilyId: kv.Key, Balance: kv.Value))
            .OrderBy(x => x.Balance)      // most negative first
            .ToList();

        var creditors = balances
            .Where(kv => kv.Value > Epsilon)
            .Select(kv => (FamilyId: kv.Key, Balance: kv.Value))
            .OrderByDescending(x => x.Balance) // largest first
            .ToList();

        var transfers = new List<Transfer>();

        int di = 0, ci = 0;

        // Use arrays for fast in-place balance mutation.
        var debtorBal = debtors.Select(x => x.Balance).ToArray();
        var creditorBal = creditors.Select(x => x.Balance).ToArray();

        while (di < debtors.Count && ci < creditors.Count)
        {
            var debtAmt = -debtorBal[di];   // positive amount owed
            var creditAmt = creditorBal[ci];  // positive amount due

            var transfer = Math.Round(Math.Min(debtAmt, creditAmt), 2, MidpointRounding.AwayFromZero);

            transfers.Add(new Transfer(
                debtors[di].FamilyId,
                creditors[ci].FamilyId,
                transfer));

            debtorBal[di] += transfer;
            creditorBal[ci] -= transfer;

            if (Math.Abs(debtorBal[di]) < Epsilon) di++;
            if (Math.Abs(creditorBal[ci]) < Epsilon) ci++;
        }

        return transfers;
    }
}
