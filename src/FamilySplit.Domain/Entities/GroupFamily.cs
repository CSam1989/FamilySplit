using FamilySplit.Domain.Enums;

namespace FamilySplit.Domain.Entities;

/// <summary>
/// Junction between a Group and a Family.
/// When a Family joins a Group, all active FamilyMembers of that Family participate.
/// One family holds the Admin role (the group creator); others are Members.
/// </summary>
public class GroupFamily
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid FamilyId { get; set; }
    public MemberRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Group Group { get; set; } = default!;
    public Family Family { get; set; } = default!;
}
