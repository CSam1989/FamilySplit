namespace FamilySplit.Domain.Entities;

/// <summary>
/// A household / family unit. Managed by one of its adult members (the family admin).
/// Multiple families can participate in a shared Group (e.g., a holiday trip).
/// All active FamilyMembers of a Family automatically participate when
/// the Family joins a Group.
/// </summary>
public class Family
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>
    /// The User who created this family (typically the global admin or the first
    /// adult member to log in). Used for audit; not the same as the family admin role.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public User? CreatedBy { get; set; }
    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
    public ICollection<GroupFamily> GroupFamilies { get; set; } = new List<GroupFamily>();
}
