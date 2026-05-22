namespace FamilySplit.Domain.Entities;

public class ExpenseParticipant
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid FamilyMemberId { get; set; }
    /// <summary>Snapshotted at expense save time. Never recalculated.</summary>
    public decimal WeightSnapshot { get; set; }
    public decimal CalculatedAmount { get; set; }
    /// <summary>Soft-exclude: row is preserved for audit; just not included in the split.</summary>
    public bool IsExcluded { get; set; }

    public Expense Expense { get; set; } = default!;
    public FamilyMember FamilyMember { get; set; } = default!;
}
