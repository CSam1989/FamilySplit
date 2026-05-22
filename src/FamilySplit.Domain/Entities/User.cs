using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = default!;
    public Provider Provider { get; set; }
    public string Email { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Global administrator flag. Global admins can create and manage any Family
    /// and FamilyMember, bypassing family-ownership checks.
    /// Set directly in the database; not self-assignable.
    /// </summary>
    public bool IsGlobalAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The FamilyMember record this user is linked to. Null if not yet linked.</summary>
    public FamilyMember? FamilyMember { get; set; }
}
