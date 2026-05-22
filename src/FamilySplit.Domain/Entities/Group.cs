namespace FamilySplit.Domain.Entities;

public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string InviteCode { get; set; } = default!;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User CreatedBy { get; set; } = default!;

    /// <summary>Families participating in this group.</summary>
    public ICollection<GroupFamily> GroupFamilies { get; set; } = new List<GroupFamily>();

    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
