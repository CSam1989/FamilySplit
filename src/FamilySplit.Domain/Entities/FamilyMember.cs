namespace FamilySplit.Domain.Entities;

public class FamilyMember
{
    public Guid Id { get; set; }

    /// <summary>The household this member belongs to.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// Email address used to link this family member to a User account when
    /// they log in via OAuth. Null for children / members who will never log in.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Set when the person with <see cref="Email"/> first logs in and the accounts
    /// are linked. Null for unlinked (child / passive) members.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// True if this member is the admin of their Family (can add/update/remove
    /// other members of the same family). Typically an adult who has a linked User.
    /// </summary>
    public bool IsAdmin { get; set; }

    public string DisplayName { get; set; } = default!;
    public DateOnly? DateOfBirth { get; set; }
    public decimal? WeightOverride { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Family Family { get; set; } = default!;
    public User? User { get; set; }
}
