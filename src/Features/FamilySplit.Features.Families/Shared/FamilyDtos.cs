using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Families.Shared;

// ── Response DTOs ─────────────────────────────────────────────────────────────
// The Families slice owns its copies of these (identical JSON property names to the legacy
// Families DTOs and to the Admin slice's own copies — the read-side integration tests lock
// the wire format). The duplication across slices is by design (per-slice DTOs).

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
