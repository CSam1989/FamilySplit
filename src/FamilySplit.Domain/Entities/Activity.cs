using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    /// <summary>Null = top-level activity. Set = sub-activity. Depth capped at 1.</summary>
    public Guid? ParentActivityId { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.Open;
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Group Group { get; set; } = default!;
    public Activity? ParentActivity { get; set; }
    public ICollection<Activity> SubActivities { get; set; } = new List<Activity>();
    public ICollection<ActivityParticipant> Participants { get; set; } = new List<ActivityParticipant>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
