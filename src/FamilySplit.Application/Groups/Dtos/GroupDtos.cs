using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Groups.Dtos;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateGroupRequest(string Name, string? Description);

public record UpdateGroupRequest(string Name, string? Description);

public record JoinGroupRequest(string InviteCode);

// ── Responses ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight summary shown in the group list.
/// FamilyCount = number of families currently in the group.
/// CallerFamilyRole = the role of the caller's family (Admin or Member).
/// </summary>
public record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string InviteCode,
    int FamilyCount,
    MemberRole CallerFamilyRole,
    DateTimeOffset CreatedAt);

/// <summary>
/// Full group detail including all participating families and their members.
/// </summary>
public record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string InviteCode,
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
