namespace FamilySplit.Client.Services;

// ── Requests ──────────────────────────────────────────────────────────────────

public record UpdateFamilyNameRequest(string Name);

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

// ── Response ──────────────────────────────────────────────────────────────────

public record FamilyDto(
    Guid Id,
    string Name,
    IReadOnlyList<FamilyMemberDto> Members,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
