using FamilySplit.Domain.Enums;

namespace FamilySplit.Client.Services;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateGroupRequest(string Name, string? Description);

public record UpdateGroupRequest(string Name, string? Description);

public record JoinGroupRequest(string InviteCode);

// ── Responses ─────────────────────────────────────────────────────────────────

public record GroupSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string InviteCode,
    int FamilyCount,
    MemberRole CallerFamilyRole,
    DateTimeOffset CreatedAt);

public record GroupDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string InviteCode,
    MemberRole CallerFamilyRole,
    IReadOnlyList<GroupFamilyDto> Families,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A family participating in a group, with its active members.</summary>
public record GroupFamilyDto(
    Guid FamilyId,
    string FamilyName,
    MemberRole Role,
    DateTimeOffset JoinedAt,
    IReadOnlyList<GroupMemberSummaryDto> Members);

/// <summary>Lightweight member summary shown inside a group's family list.</summary>
public record GroupMemberSummaryDto(
    Guid Id,
    string DisplayName,
    decimal CurrentWeight,
    WeightTier CurrentTier,
    bool IsLinked);
