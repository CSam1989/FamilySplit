using System.Security.Cryptography;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Auth;

/// <summary>
/// Issues, rotates, and revokes long-lived refresh tokens. The secret value is
/// returned to the caller exactly once (at <see cref="IssueAsync"/> or
/// <see cref="RotateAsync"/> time) and never stored on the server — only its
/// SHA-256 hash is persisted.
///
/// Rotation behaviour: every successful refresh marks the presented token as
/// <c>RevokedAt = now</c> and points <c>ReplacedByTokenId</c> at the new row.
/// If a refresh attempt presents a token that is already revoked, that is
/// treated as theft and <see cref="RevokeAllForUserAsync"/> is invoked.
/// </summary>
public class RefreshTokenService
{
    private const int SecretByteLength = 32; // 256 bits

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        AppDbContext db,
        IConfiguration config,
        ILogger<RefreshTokenService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public TimeSpan TokenLifetime
    {
        get
        {
            var days = int.TryParse(_config["Jwt:RefreshLifetimeDays"], out var configuredDays)
                ? configuredDays
                : 30;
            return TimeSpan.FromDays(days);
        }
    }

    // ── Issue a fresh token (first login) ─────────────────────────────────

    public async Task<IssuedToken> IssueAsync(
        Guid userId,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        var (secret, hash) = NewSecret();
        var now = DateTimeOffset.UtcNow;

        var entity = new RefreshToken
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            TokenHash     = hash,
            CreatedAt     = now,
            ExpiresAt     = now + TokenLifetime,
            CreatedFromIp = Trim(ip, 45),
            UserAgent     = Trim(userAgent, 512),
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Refresh token issued for user {UserId} (tokenId: {TokenId}, expires: {ExpiresAt})",
            userId, entity.Id, entity.ExpiresAt);

        return new IssuedToken(secret, entity.ExpiresAt);
    }

    // ── Rotate: validate + revoke old + issue new ─────────────────────────

    /// <summary>
    /// Validates the presented refresh-cookie secret. On success the row is
    /// rotated and a new (secret, expiry, userId) tuple is returned. Returns
    /// null when the secret is malformed, unknown, expired, or revoked.
    ///
    /// A presented token that maps to an already-revoked row is treated as
    /// replay / theft — every active token for that user is revoked before
    /// returning null.
    /// </summary>
    public async Task<RotatedToken?> RotateAsync(
        string presentedSecret,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presentedSecret)) return null;

        byte[] hash;
        try { hash = Sha256(presentedSecret); }
        catch { return null; }

        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null) return null;

        // Theft detection: presented token already revoked → kill the family.
        if (existing.RevokedAt is not null)
        {
            _logger.LogWarning(
                "Refresh-token replay detected for user {UserId}; revoking all active tokens.",
                existing.UserId);
            await RevokeAllForUserAsync(existing.UserId, ct);
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        if (existing.ExpiresAt <= now) return null;

        // Issue the replacement first so we can point at it.
        var (newSecret, newHash) = NewSecret();
        var replacement = new RefreshToken
        {
            Id            = Guid.NewGuid(),
            UserId        = existing.UserId,
            TokenHash     = newHash,
            CreatedAt     = now,
            ExpiresAt     = now + TokenLifetime,
            CreatedFromIp = Trim(ip, 45),
            UserAgent     = Trim(userAgent, 512),
        };
        _db.RefreshTokens.Add(replacement);

        existing.RevokedAt         = now;
        existing.ReplacedByTokenId = replacement.Id;

        await _db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Refresh token rotated for user {UserId} (old: {OldTokenId} → new: {NewTokenId}, expires: {ExpiresAt})",
            existing.UserId, existing.Id, replacement.Id, replacement.ExpiresAt);

        return new RotatedToken(existing.UserId, newSecret, replacement.ExpiresAt);
    }

    // ── Revoke a single presented token (logout) ──────────────────────────

    public async Task RevokeAsync(string? presentedSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presentedSecret)) return;

        byte[] hash;
        try { hash = Sha256(presentedSecret); }
        catch { return; }

        var row = await _db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .FirstOrDefaultAsync(ct);

        if (row is null) return;

        row.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Refresh token revoked for user {UserId} (tokenId: {TokenId})", row.UserId, row.Id);
    }

    // ── Revoke all active tokens for a user (theft, password change) ──────

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var revoked = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        _logger.LogInformation(
            "All refresh tokens revoked for user {UserId} ({Count} token(s) invalidated)",
            userId, revoked);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (string secret, byte[] hash) NewSecret()
    {
        Span<byte> bytes = stackalloc byte[SecretByteLength];
        RandomNumberGenerator.Fill(bytes);

        // URL-safe base64 with no padding — safe for cookies and headers.
        var secret = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (secret, Sha256(secret));
    }

    private static byte[] Sha256(string value)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value), hash);
        return hash.ToArray();
    }

    private static string? Trim(string? s, int max) =>
        s is null || s.Length <= max ? s : s[..max];

    public sealed record IssuedToken(string Secret, DateTimeOffset ExpiresAt);
    public sealed record RotatedToken(Guid UserId, string Secret, DateTimeOffset ExpiresAt);
}
