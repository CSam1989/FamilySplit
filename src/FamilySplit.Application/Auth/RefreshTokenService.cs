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
///
/// Reuse window: if the presented token was issued within <see cref="ReuseWindow"/>
/// (default 60 min, configurable via <c>Jwt:RefreshReuseWindowMinutes</c>), the
/// existing row is left untouched and a <see cref="RotateResult.Reused"/> result is
/// returned so the caller can issue a fresh JWT without updating the cookie.  Full
/// rotation still happens once per reuse window, limiting table growth to roughly
/// <c>24 / (ReuseWindowMinutes / 60)</c> rows per user per day.
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

    /// <summary>
    /// How long a refresh token can be reused without triggering a rotation.
    /// Within this window the existing row is left intact and only a new JWT is
    /// issued, dramatically reducing <c>refresh_tokens</c> table churn.
    /// Configure via <c>Jwt:RefreshReuseWindowMinutes</c> (default: 60).
    /// </summary>
    public TimeSpan ReuseWindow
    {
        get
        {
            var minutes = int.TryParse(_config["Jwt:RefreshReuseWindowMinutes"], out var m) ? m : 60;
            return TimeSpan.FromMinutes(minutes);
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
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now + TokenLifetime,
            CreatedFromIp = Trim(ip, 45),
            UserAgent = Trim(userAgent, 512),
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
    /// rotated and a <see cref="RotateResult.Success"/> outcome is returned.
    /// <para>
    /// Two distinct failure outcomes are distinguished so the caller can decide
    /// whether to clear the browser cookie:
    /// <list type="bullet">
    ///   <item><see cref="RotateResult.Rejected"/> – unknown / expired / genuine
    ///   theft.  The caller should clear the cookie.</item>
    ///   <item><see cref="RotateResult.ConcurrentRetry"/> – the token was already
    ///   rotated but its replacement is still active, meaning two refresh requests
    ///   raced and the second arrived after the first was already processed.  The
    ///   caller must <em>not</em> clear the cookie — the browser already holds the
    ///   correct replacement cookie from the first response.</item>
    /// </list>
    /// </para>
    /// </summary>
    public async Task<RotateResult> RotateAsync(
        string presentedSecret,
        string? ip,
        string? userAgent,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(presentedSecret)) return RotateResult.RejectedInstance;

        byte[] hash;
        try { hash = Sha256(presentedSecret); }
        catch { return RotateResult.RejectedInstance; }

        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null) return RotateResult.RejectedInstance;

        // Token already revoked — distinguish concurrent retry from genuine theft.
        if (existing.RevokedAt is not null)
        {
            // If the token has an active replacement the most likely cause is a
            // concurrent-retry race: the client sent two refresh requests before
            // the first response arrived and updated the cookie.  The browser
            // already holds the replacement cookie from the winning response, so
            // we must NOT clear the cookie here — doing so would remove the only
            // valid cookie the browser has.
            if (existing.ReplacedByTokenId is not null)
            {
                var replacementActive = await _db.RefreshTokens
                    .AnyAsync(t => t.Id == existing.ReplacedByTokenId && t.RevokedAt == null, ct);

                if (replacementActive)
                {
                    _logger.LogInformation(
                        "Concurrent refresh detected for user {UserId} — stale token {OldTokenId} " +
                        "reused while replacement is still active; ignoring (not treated as theft).",
                        existing.UserId, existing.Id);
                    return RotateResult.ConcurrentRetryInstance;
                }
            }

            // No active replacement → the full rotation chain is compromised.
            // This is genuine theft or a severe client bug; kill all sessions.
            _logger.LogWarning(
                "Refresh-token replay detected for user {UserId}; revoking all active tokens.",
                existing.UserId);
            await RevokeAllForUserAsync(existing.UserId, ct);
            return RotateResult.RejectedInstance;
        }

        var now = DateTimeOffset.UtcNow;
        if (existing.ExpiresAt <= now) return RotateResult.RejectedInstance;

        // Reuse window: skip rotation when the token was issued recently.
        // The browser keeps its existing cookie; the endpoint issues a fresh JWT.
        // This cuts table growth from "one row per page-load" to ~1 row/hour/user.
        if (now - existing.CreatedAt < ReuseWindow)
        {
            _logger.LogDebug(
                "Refresh token reused for user {UserId} (tokenId: {TokenId}, age: {AgeMinutes}min)",
                existing.UserId, existing.Id, (int)(now - existing.CreatedAt).TotalMinutes);
            return new RotateResult.Reused(existing.UserId);
        }

        // Issue the replacement first so we can point at it.
        var (newSecret, newHash) = NewSecret();
        var replacement = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now + TokenLifetime,
            CreatedFromIp = Trim(ip, 45),
            UserAgent = Trim(userAgent, 512),
        };
        _db.RefreshTokens.Add(replacement);

        existing.RevokedAt = now;
        existing.ReplacedByTokenId = replacement.Id;

        await _db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Refresh token rotated for user {UserId} (old: {OldTokenId} → new: {NewTokenId}, expires: {ExpiresAt})",
            existing.UserId, existing.Id, replacement.Id, replacement.ExpiresAt);

        return new RotateResult.Success(existing.UserId, newSecret, replacement.ExpiresAt);
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

    // ── Prune old rows (call periodically to keep the table small) ───────

    /// <summary>
    /// Deletes refresh-token rows that are both revoked/expired and older than
    /// <paramref name="retentionDays"/> (default 7 days).  The retention window
    /// exists so that <see cref="RotateResult.ConcurrentRetry"/> detection keeps
    /// working for any in-flight race that landed before the prune ran.
    /// </summary>
    public async Task<int> PruneExpiredAsync(int retentionDays = 7, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(retentionDays);

        var deleted = await _db.RefreshTokens
            .Where(t => (t.RevokedAt != null && t.RevokedAt < cutoff)
                     || (t.RevokedAt == null && t.ExpiresAt < cutoff))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            _logger.LogInformation("Pruned {Count} expired/revoked refresh token(s)", deleted);

        return deleted;
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

    /// <summary>
    /// Discriminated result from <see cref="RotateAsync"/>. The caller must inspect
    /// the concrete type to decide whether to write a new cookie, leave the existing
    /// cookie in place, or clear it.
    /// </summary>
    public abstract record RotateResult
    {
        private RotateResult() { }

        /// <summary>Rotation succeeded — write the new cookie and issue a JWT.</summary>
        public sealed record Success(Guid UserId, string Secret, DateTimeOffset ExpiresAt) : RotateResult;

        /// <summary>
        /// Token is within the reuse window — issue a fresh JWT without rotating.
        /// The caller must not touch the cookie; the browser already holds the
        /// correct (still-active) token.
        /// </summary>
        public sealed record Reused(Guid UserId) : RotateResult;

        /// <summary>
        /// Token was not found, was expired, or genuine theft was detected (all
        /// sessions revoked).  The caller should clear the refresh cookie.
        /// </summary>
        public sealed record Rejected : RotateResult;

        /// <summary>
        /// Token was already rotated but its replacement is still active — almost
        /// certainly a concurrent-retry race.  The caller must <em>not</em> clear
        /// the cookie; the browser already holds the correct replacement cookie.
        /// </summary>
        public sealed record ConcurrentRetry : RotateResult;

        /// <summary>Singleton for the common <see cref="Rejected"/> case.</summary>
        public static readonly Rejected RejectedInstance = new();

        /// <summary>Singleton for the <see cref="ConcurrentRetry"/> case.</summary>
        public static readonly ConcurrentRetry ConcurrentRetryInstance = new();
    }
}
