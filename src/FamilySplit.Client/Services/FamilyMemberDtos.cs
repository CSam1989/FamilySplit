using FamilySplit.Domain.Enums;

namespace FamilySplit.Client.Services;

// ── Response ──────────────────────────────────────────────────────────────────

/// <summary>
/// Profile of a single FamilyMember — returned by /users/me/profile
/// and embedded inside FamilyDto.
/// </summary>
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
    bool IsAdmin,
    DateTimeOffset CreatedAt);
