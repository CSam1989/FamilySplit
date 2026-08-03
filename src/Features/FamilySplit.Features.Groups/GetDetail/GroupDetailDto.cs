using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Groups.GetDetail;

/// <summary>
/// Full group detail including all participating families and their members.
/// </summary>
public record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string? InviteCode, // null for non-admin families — the code controls who can join
    MemberRole CallerFamilyRole,
    IReadOnlyList<GroupFamilyDto> Families,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// A family participating in a group, with a snapshot of its active members.
/// </summary>
public record GroupFamilyDto(
    Guid FamilyId,
    string FamilyName,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    IReadOnlyList<GroupMemberSummaryDto> Members);

/// <summary>
/// Lightweight member summary used inside GroupFamilyDto.
/// </summary>
public record GroupMemberSummaryDto(
    Guid Id,
    string DisplayName,
    decimal CurrentWeight,
    WeightTier CurrentTier,
    bool IsLinked);
