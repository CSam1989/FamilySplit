using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class ApprovalStep
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Guid ApproverId { get; set; }
    public StepType StepType { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public DateTimeOffset? ActionedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Settlement Settlement { get; set; } = default!;
    public User Approver { get; set; } = default!;
}
