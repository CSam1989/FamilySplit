namespace FamilySplit.Domain.Entities;

/// <summary>
/// Server-side record of a long-lived refresh token issued to a browser session.
///
/// Only the SHA-256 hash of the secret value is stored — the secret itself lives
/// solely in the HttpOnly cookie on the user's device. On every refresh request
/// the presented secret is hashed and compared to <see cref="TokenHash"/>.
///
/// Tokens are <b>rotated</b> on each use: the row is marked
/// <see cref="RevokedAt"/> and <see cref="ReplacedByTokenId"/> is set to the new
/// row. A refresh attempt that presents a token already revoked indicates either
/// replay or theft — in that case every active token for the user is revoked.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the secret cookie value. 32 bytes.</summary>
    public byte[] TokenHash { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when this token has been rotated, revoked, or used for logout.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The token that supersedes this one after rotation. Null until rotated.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>Source IP captured at issue time for forensic purposes only.</summary>
    public string? CreatedFromIp { get; set; }

    /// <summary>User-Agent captured at issue time for forensic purposes only.</summary>
    public string? UserAgent { get; set; }

    // Navigation property — optional, for cascade delete on user removal.
    public User? User { get; set; }
}
