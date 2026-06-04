using System.Security.Cryptography;
using System.Text;
using FamilySplit.IntegrationTests.Infrastructure;
using Npgsql;

namespace FamilySplit.IntegrationTests.Auth;

// ---------------------------------------------------------------------------
// Shared base — refresh-token seeding helpers
// ---------------------------------------------------------------------------

public abstract class AuthTestBase : IntegrationTestBase
{
    private const string RefreshCookieName = "fs_refresh";

    protected AuthTestBase(PostgresContainerFixture fixture) : base(fixture) { }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a refresh_tokens row using the shared connection.
    /// Returns (plaintext secret, tokenId). The secret is never stored — only
    /// its SHA-256 hash is persisted, mirroring production behaviour.
    /// </summary>
    protected async Task<(string secret, Guid tokenId)> SeedRefreshTokenAsync(
        Guid userId,
        double createdAtHoursAgo = 2.0,   // 2h past the default 60-min reuse window
        bool alreadyRevoked = false,
        Guid? replacedByTokenId = null,
        bool expired = false,
        CancellationToken ct = default)
    {
        var tokenId = Guid.NewGuid();
        var secret = GenerateSecret();
        var hashBytes = ComputeSha256(secret);
        var now = DateTimeOffset.UtcNow;
        var createdAt = now - TimeSpan.FromHours(createdAtHoursAgo);
        var expiresAt = expired
            ? now - TimeSpan.FromMinutes(1)
            : now + TimeSpan.FromDays(30);
        DateTimeOffset? revokedAt = alreadyRevoked
            ? now - TimeSpan.FromMinutes(5)
            : null;

        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO refresh_tokens
                (id, user_id, token_hash, created_at, expires_at, revoked_at, replaced_by_token_id,
                 created_from_ip, user_agent)
            VALUES
                (@id, @userId, @hash, @createdAt, @expiresAt, @revokedAt, @replacedBy,
                 @ip, @ua)
            """;
        cmd.Parameters.AddWithValue("id", tokenId);
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", hashBytes);
        cmd.Parameters.AddWithValue("createdAt", createdAt);
        cmd.Parameters.AddWithValue("expiresAt", expiresAt);
        cmd.Parameters.AddWithValue("revokedAt", revokedAt.HasValue ? (object)revokedAt.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("replacedBy", replacedByTokenId.HasValue
            ? (object)replacedByTokenId.Value
            : DBNull.Value);
        cmd.Parameters.AddWithValue("ip", DBNull.Value);
        cmd.Parameters.AddWithValue("ua", "integration-test-agent");
        await cmd.ExecuteNonQueryAsync(ct);

        return (secret, tokenId);
    }

    /// <summary>
    /// Returns the number of active (non-revoked, non-expired) refresh token rows for a user.
    /// </summary>
    protected async Task<int> CountActiveTokensAsync(Guid userId, CancellationToken ct = default)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM refresh_tokens
            WHERE user_id = @userId AND revoked_at IS NULL AND expires_at > now()
            """;
        cmd.Parameters.AddWithValue("userId", userId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Reads back a single refresh_tokens row by id; returns null if not found.
    /// </summary>
    protected async Task<(DateTimeOffset? revokedAt, Guid? replacedByTokenId, byte[] tokenHash)?>
        GetRefreshTokenRowAsync(Guid tokenId, CancellationToken ct = default)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = """
            SELECT revoked_at, replaced_by_token_id, token_hash
            FROM refresh_tokens
            WHERE id = @id
            """;
        cmd.Parameters.AddWithValue("id", tokenId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        DateTimeOffset? revokedAt = reader.IsDBNull(0) ? null : reader.GetFieldValue<DateTimeOffset>(0);
        Guid? replacedBy = reader.IsDBNull(1) ? null : reader.GetGuid(1);
        var hashBytes = (byte[])reader["token_hash"];
        return (revokedAt, replacedBy, hashBytes);
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new HttpClient (no Auth header) with the refresh cookie preset.
    /// Uses HandleCookies=false so we set the Cookie header manually and read
    /// the Set-Cookie header from responses without a cookie jar interfering.
    /// </summary>
    protected HttpClient CreateClientWithRefreshCookie(string secret)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", $"fs_refresh={secret}");
        return client;
    }

    // ── Crypto helpers ────────────────────────────────────────────────────────

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    protected static byte[] ComputeSha256(string value)
    {
        var hash = new byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return hash;
    }

    /// <summary>Extracts the value of a named cookie from a Set-Cookie response header.</summary>
    protected static string? ExtractSetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            return null;

        foreach (var header in setCookieHeaders)
        {
            if (!header.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase))
                continue;

            // The value is the first segment before "; "
            var valueSegment = header[(cookieName.Length + 1)..];
            var semicolonIndex = valueSegment.IndexOf(';');
            return semicolonIndex == -1 ? valueSegment : valueSegment[..semicolonIndex];
        }

        return null;
    }
}

// ---------------------------------------------------------------------------
// POST /auth/refresh — token rotation
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RefreshRotationTests : AuthTestBase
{
    public RefreshRotationTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Refresh_ValidTokenOlderThanReuseWindow_Returns200WithJwtAndRotatesRow()
    {
        // Arrange — seed a token created 2 h ago (past the default 60-min reuse window).
        var ct = TestContext.Current.CancellationToken;
        var (secret, oldTokenId) = await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 2.0, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert — 200 with a JWT
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("expiresInSeconds").GetInt32().Should().BePositive();

        // Old row must be revoked and point at its replacement.
        var oldRow = await GetRefreshTokenRowAsync(oldTokenId, ct);
        oldRow.Should().NotBeNull();
        oldRow!.Value.revokedAt.Should().NotBeNull("old token must be revoked after rotation");
        oldRow.Value.replacedByTokenId.Should().NotBeNull("old token must reference its replacement");

        // A new Set-Cookie with the replacement secret must be in the response.
        var newSecret = ExtractSetCookieValue(response, "fs_refresh");
        newSecret.Should().NotBeNullOrEmpty("rotation must issue a new refresh cookie");
        newSecret.Should().NotBe(secret, "new cookie must differ from the old one");
    }

    [Fact]
    public async Task Refresh_ValidTokenWithinReuseWindow_Returns200WithJwtAndKeepsRowActive()
    {
        // Arrange — seed a token created only 1 minute ago (within the 60-min reuse window).
        var ct = TestContext.Current.CancellationToken;
        var (secret, tokenId) = await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 0.017, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert — 200 with a JWT (reuse path still issues a JWT)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();

        // The original row must still be active — reuse does NOT revoke the token.
        var row = await GetRefreshTokenRowAsync(tokenId, ct);
        row.Should().NotBeNull();
        row!.Value.revokedAt.Should().BeNull("token within reuse window must not be revoked");

        // No new Set-Cookie header — the browser keeps its existing cookie.
        var newSecret = ExtractSetCookieValue(response, "fs_refresh");
        newSecret.Should().BeNull("reuse must not set a new refresh cookie");
    }

    [Fact]
    public async Task Refresh_NoCookie_Returns401()
    {
        // Arrange — client without any Cookie header.
        var ct = TestContext.Current.CancellationToken;
        using var anonymousClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        // Act
        var response = await anonymousClient.PostAsync("/auth/refresh", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_InvalidSecret_Returns401()
    {
        // Arrange — cookie value that has no matching DB row.
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClientWithRefreshCookie("totally-bogus-secret-value");

        // Act
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        // Arrange — seed a token whose expires_at is in the past.
        var ct = TestContext.Current.CancellationToken;
        var (secret, _) = await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 2.0, expired: true, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// ---------------------------------------------------------------------------
// POST /auth/refresh — theft detection
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RefreshTheftDetectionTests : AuthTestBase
{
    public RefreshTheftDetectionTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Refresh_RevokedTokenWithNoActiveReplacement_Returns401AndRevokesAllSessions()
    {
        // Arrange — seed two active tokens for the caller, then seed a THIRD token that
        // is already revoked with no replacement. Presenting the revoked token simulates theft.
        var ct = TestContext.Current.CancellationToken;
        await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 0.1, ct: ct);  // active session 1
        await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 0.2, ct: ct);  // active session 2

        var (stolenSecret, _) = await SeedRefreshTokenAsync(
            CallerId,
            createdAtHoursAgo: 2.0,
            alreadyRevoked: true,
            replacedByTokenId: null,   // no replacement → genuine theft indicator
            ct: ct);

        var activeBeforeTheft = await CountActiveTokensAsync(CallerId, ct);
        activeBeforeTheft.Should().Be(2, "two active sessions were seeded");

        using var client = CreateClientWithRefreshCookie(stolenSecret);

        // Act — present the revoked/stolen token.
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert — 401 and all sessions killed.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activeAfterTheft = await CountActiveTokensAsync(CallerId, ct);
        activeAfterTheft.Should().Be(0, "theft detection must revoke ALL active sessions");
    }

    [Fact]
    public async Task Refresh_RevokedTokenWithActiveReplacement_Returns401ButKeepsReplacementActive()
    {
        // Arrange — seed an already-revoked token that has an active replacement.
        // This simulates a concurrent-retry race where two refresh requests were in flight.
        var ct = TestContext.Current.CancellationToken;

        // The active replacement
        var (_, replacementId) = await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 0.1, ct: ct);

        // The revoked predecessor that points at the replacement
        var (oldSecret, _) = await SeedRefreshTokenAsync(
            CallerId,
            createdAtHoursAgo: 2.0,
            alreadyRevoked: true,
            replacedByTokenId: replacementId,
            ct: ct);

        using var client = CreateClientWithRefreshCookie(oldSecret);

        // Act — present the already-rotated token (concurrent-retry scenario).
        var response = await client.PostAsync("/auth/refresh", null, ct);

        // Assert — 401 (the request cannot succeed) but the replacement must stay active.
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var activeAfter = await CountActiveTokensAsync(CallerId, ct);
        activeAfter.Should().Be(1, "concurrent-retry must not kill the active replacement session");
    }
}

// ---------------------------------------------------------------------------
// POST /auth/refresh — plaintext never stored
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class RefreshSecurityTests : AuthTestBase
{
    public RefreshSecurityTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Refresh_AfterRotation_NewTokenRowStoresHashNotPlaintext()
    {
        // Arrange — seed a token older than the reuse window so rotation occurs.
        var ct = TestContext.Current.CancellationToken;
        var (secret, oldId) = await SeedRefreshTokenAsync(CallerId, createdAtHoursAgo: 2.0, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/refresh", null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Grab the new secret from the Set-Cookie header.
        var newSecret = ExtractSetCookieValue(response, "fs_refresh");
        newSecret.Should().NotBeNullOrEmpty();

        // Find the new token row by looking at the replacement chain.
        var oldRow = await GetRefreshTokenRowAsync(oldId, ct);
        var newId = oldRow!.Value.replacedByTokenId!.Value;
        var newRow = await GetRefreshTokenRowAsync(newId, ct);
        newRow.Should().NotBeNull();

        // The stored hash must equal SHA-256(newSecret), not the raw UTF-8 bytes of newSecret.
        var expectedHash = ComputeSha256(newSecret!);
        newRow!.Value.tokenHash.Should().Equal(expectedHash,
            "only the SHA-256 hash of the secret may be stored, never the plaintext");

        var plaintextBytes = Encoding.UTF8.GetBytes(newSecret!);
        newRow.Value.tokenHash.Should().NotEqual(plaintextBytes,
            "the stored hash must not be the raw UTF-8 of the secret");
    }
}

// ---------------------------------------------------------------------------
// POST /auth/logout
// ---------------------------------------------------------------------------

[Trait("Category", "Integration")]
[Collection(nameof(IntegrationCollection))]
public sealed class LogoutTests : AuthTestBase
{
    public LogoutTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Logout_WithValidRefreshCookie_Returns204AndRevokesTokenRow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (secret, tokenId) = await SeedRefreshTokenAsync(CallerId, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/logout", null, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var row = await GetRefreshTokenRowAsync(tokenId, ct);
        row.Should().NotBeNull();
        row!.Value.revokedAt.Should().NotBeNull("logout must mark the token row as revoked");
    }

    [Fact]
    public async Task Logout_WithNoCookie_Returns204Gracefully()
    {
        // Arrange — no Cookie header at all.
        var ct = TestContext.Current.CancellationToken;
        using var anonymousClient = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        // Act
        var response = await anonymousClient.PostAsync("/auth/logout", null, ct);

        // Assert — logout is idempotent; missing cookie is not an error.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_ClearsRefreshCookie_InSetCookieHeader()
    {
        // Arrange — seed a token so logout has something to revoke.
        var ct = TestContext.Current.CancellationToken;
        var (secret, _) = await SeedRefreshTokenAsync(CallerId, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        // Act
        var response = await client.PostAsync("/auth/logout", null, ct);

        // Assert — the response must instruct the browser to delete the cookie.
        // A Set-Cookie header with an empty value or a past Expires clears the cookie.
        var setCookieHeaders = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToList()
            : new List<string>();

        var refreshCookieCleared = setCookieHeaders.Any(h =>
            h.Contains("fs_refresh=", StringComparison.OrdinalIgnoreCase) &&
            (h.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)
             || h.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)
             || h.Contains("fs_refresh=;", StringComparison.OrdinalIgnoreCase)));

        refreshCookieCleared.Should().BeTrue(
            "logout must clear the fs_refresh cookie via a Set-Cookie header");
    }

    [Fact]
    public async Task Logout_ThenRefresh_Returns401()
    {
        // Arrange — issue and then immediately revoke a token.
        var ct = TestContext.Current.CancellationToken;
        var (secret, _) = await SeedRefreshTokenAsync(CallerId, ct: ct);
        using var client = CreateClientWithRefreshCookie(secret);

        var logoutResponse = await client.PostAsync("/auth/logout", null, ct);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — try to refresh with the now-revoked token.
        var refreshResponse = await client.PostAsync("/auth/refresh", null, ct);

        // Assert — revoked token must not yield a new JWT.
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
