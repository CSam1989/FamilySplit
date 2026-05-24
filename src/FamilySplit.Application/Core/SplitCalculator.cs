using FamilySplit.Domain.Entities;

namespace FamilySplit.Application.Core;

/// <summary>
/// Computes the per-participant CalculatedAmount for an expense based on
/// non-excluded WeightSnapshots. The rounding remainder (max ±0.01) is applied
/// to the participant with the largest snapshot weight.
/// </summary>
public class SplitCalculator
{
    /// <summary>
    /// Distributes <paramref name="totalAmount"/> across all non-excluded
    /// <paramref name="participants"/> in proportion to their
    /// <see cref="ExpenseParticipant.WeightSnapshot"/>.
    /// Excluded participants receive CalculatedAmount = 0.
    /// </summary>
    public static void CalculateShares(decimal totalAmount, IList<ExpenseParticipant> participants)
    {
        var active = participants.Where(p => !p.IsExcluded).ToList();

        // Zero out excluded participants.
        foreach (var p in participants.Where(p => p.IsExcluded))
            p.CalculatedAmount = 0m;

        if (active.Count == 0) return;

        var totalWeight = active.Sum(p => p.WeightSnapshot);
        if (totalWeight == 0m)
        {
            // Edge case: all weights are 0 — split equally.
            var equal = Math.Round(totalAmount / active.Count, 2, MidpointRounding.AwayFromZero);
            foreach (var p in active) p.CalculatedAmount = equal;
            var equalRemainder = totalAmount - equal * active.Count;
            active[0].CalculatedAmount += equalRemainder;
            return;
        }

        decimal allocated = 0m;
        ExpenseParticipant? heaviest = null;
        decimal maxWeight = -1m;

        foreach (var p in active)
        {
            var share = Math.Round(p.WeightSnapshot / totalWeight * totalAmount, 2, MidpointRounding.AwayFromZero);
            p.CalculatedAmount = share;
            allocated += share;

            if (p.WeightSnapshot > maxWeight)
            {
                maxWeight = p.WeightSnapshot;
                heaviest  = p;
            }
        }

        // Absorb any rounding remainder (max ±0.01) into the largest-share participant.
        if (heaviest is not null && allocated != totalAmount)
            heaviest.CalculatedAmount += totalAmount - allocated;
    }
}
