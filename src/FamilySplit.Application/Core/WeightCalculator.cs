using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Core;

/// <summary>
/// Resolves an effective weight for a <see cref="FamilyMember"/> at a given expense date.
/// Order of precedence: WeightOverride → age-tier from DOB → 1.00 fallback.
/// The numeric result is snapshotted on ExpenseParticipant.WeightSnapshot at save time and never re-derived.
/// </summary>
public static class WeightCalculator
{
    public static decimal GetWeight(FamilyMember member, DateOnly expenseDate)
    {
        if (member.WeightOverride.HasValue) return member.WeightOverride.Value;
        if (member.DateOfBirth is null) return 1.00m;

        int age = expenseDate.Year - member.DateOfBirth.Value.Year;
        if (expenseDate < member.DateOfBirth.Value.AddYears(age)) age--;

        return age switch
        {
            < 6 => 0.25m, // kleuterschool
            < 12 => 0.50m, // lager onderwijs
            < 18 => 0.75m, // middelbaar onderwijs
            _ => 1.00m  // volwassene
        };
    }

    public static WeightTier GetTier(FamilyMember member, DateOnly referenceDate)
    {
        if (member.WeightOverride.HasValue) return WeightTier.Override;
        if (member.DateOfBirth is null) return WeightTier.Volwassene;

        int age = referenceDate.Year - member.DateOfBirth.Value.Year;
        if (referenceDate < member.DateOfBirth.Value.AddYears(age)) age--;

        return age switch
        {
            < 6 => WeightTier.Kleuterschool,
            < 12 => WeightTier.LagerOnderwijs,
            < 18 => WeightTier.MiddelbaarOnderwijs,
            _ => WeightTier.Volwassene
        };
    }
}
