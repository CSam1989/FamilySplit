namespace FamilySplit.Domain.Enums;

/// <summary>
/// Age-based weight tiers modelled on the Belgian school system.
/// Used purely for display; the numeric weight is what gets snapshotted on ExpenseParticipant.
/// </summary>
public enum WeightTier
{
    Kleuterschool,       // < 6      => 0.25
    LagerOnderwijs,      // 6 – 11   => 0.50
    MiddelbaarOnderwijs, // 12 – 17  => 0.75
    Volwassene,          // 18+      => 1.00
    Override             // weight_override set; tier ignored
}
