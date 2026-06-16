namespace FamilySplit.Features.Users.WhoAmI;

/// <summary>
/// The authenticated caller's identity, returned by <c>GET /whoami</c>. Mirrors the field
/// shape the legacy inline endpoint projected (the read-side wire-format lock) — <c>Provider</c>
/// is serialised as its string name.
/// </summary>
public sealed record WhoAmIDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string Provider,
    DateTimeOffset CreatedAt,
    bool IsGlobalAdmin);
