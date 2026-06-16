using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Groups.List;

/// <summary>
/// Lightweight summary shown in the group list.
/// FamilyCount = number of families currently in the group.
/// CallerFamilyRole = the role of the caller's family (Admin or Member).
/// </summary>
public record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string? InviteCode, // null for non-admin families — the code controls who can join
    int FamilyCount,
    MemberRole CallerFamilyRole,
    DateTimeOffset CreatedAt);
