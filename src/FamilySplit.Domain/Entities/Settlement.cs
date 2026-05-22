using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class Settlement
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid PayerUserId { get; set; }
    public Guid ReceiverUserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public SettlementStatus Status { get; set; } = SettlementStatus.Proposed;
    public string? Notes { get; set; }
    public DateTimeOffset ProposedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Activity Activity { get; set; } = default!;
    public User Payer { get; set; } = default!;
    public User Receiver { get; set; } = default!;
    public ICollection<ApprovalStep> ApprovalSteps { get; set; } = new List<ApprovalStep>();
}
