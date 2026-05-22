using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Families.Dtos;

// ── Requests ──────────────────────────────────────────────────────────────────

public record UpdateFamilyNameRequest(string Name);

/// <summary>Sent when adding a new member to a family.</summary>
public record AddFamilyMemberRequest(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);

public record UpdateFamilyMemberRequest(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);

// ── Responses ─────────────────────────────────────────────────────────────────

public record FamilyMemberDto(
    Guid Id,
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    decimal CurrentWeight,
    WeightTier CurrentTier,
    bool IsActive,
    /// <summary>True when the member has linked their User account by logging in.</summary>
    bool IsLinked,
    /// <summary>True when the member can manage their family (add/edit/remove other members).</summary>
    bool IsAdmin,
    DateTimeOffset CreatedAt);

public record FamilyDto(
    Guid Id,
    string Name,
    IReadOnlyList<FamilyMemberDto> Members,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
