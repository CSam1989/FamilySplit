using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class Settlement
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }

    /// <summary>The Family that owes money and must transfer it.</summary>
    public Guid PayerFamilyId { get; set; }

    /// <summary>The Family that is owed money and will receive the transfer.</summary>
    public Guid ReceiverFamilyId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public SettlementStatus Status { get; set; } = SettlementStatus.Proposed;
    public string? Notes { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Activity Activity { get; set; } = default!;
    public Family PayerFamily { get; set; } = default!;
    public Family ReceiverFamily { get; set; } = default!;
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
}
