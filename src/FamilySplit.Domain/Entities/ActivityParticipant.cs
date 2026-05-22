namespace FamilySplit.Domain.Entities;

public class ActivityParticipant
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid FamilyMemberId { get; set; }

    public Activity Activity { get; set; } = default!;
    public FamilyMember FamilyMember { get; set; } = default!;
}
