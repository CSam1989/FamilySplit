using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace FamilySplit.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E tests. Provides:
/// <list type="bullet">
///   <item>A Playwright <see cref="IBrowser"/> and per-test <see cref="IBrowserContext"/>.</item>
///   <item>Helpers to seed a test user and pre-authenticate via a refresh-token cookie.</item>
///   <item>Auto-skip when the client server is unavailable.</item>
/// </list>
///
/// <b>Server requirements:</b>
/// The API (<see cref="E2EApiServer"/>) and optionally the client (<see cref="E2EClientServer"/>)
/// must be started before tests run.  These are managed by the <see cref="E2ECollection"/>
/// collection fixture which xUnit starts once per test collection.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly E2EApiServer _apiServer;
    private readonly E2EClientServer _clientServer;

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    // Exposed to subclasses for navigation and interaction.
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    // Set during InitializeAsync — subclasses can read these for assertions.
    protected Guid TestUserId { get; private set; }
    protected Guid TestMemberId { get; private set; }
    protected Guid TestFamilyId { get; private set; }

    protected E2ETestBase(E2EApiServer apiServer, E2EClientServer clientServer)
    {
        _apiServer = apiServer;
        _clientServer = clientServer;
    }

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async ValueTask InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });

        // Seed a test user + family for this test.
        (TestUserId, TestMemberId, TestFamilyId) = await SeedTestUserAsync();

        // Create an unauthenticated context by default.
        // Call AuthenticateContextAsync() to add the refresh cookie.
        Context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2EConfig.ClientBaseUrl,
            IgnoreHTTPSErrors = true,
        });

        Page = await Context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Page is not null)
            await Page.CloseAsync();

        if (Context is not null)
            await Context.CloseAsync();

        if (_browser is not null)
            await _browser.DisposeAsync();

        _playwright?.Dispose();
    }

    // ── Auth helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a valid fs_refresh cookie to the current <see cref="Context"/> so that
    /// when the Blazor WASM app boots, its silent-refresh call succeeds and the user
    /// is treated as authenticated without going through Google OAuth.
    ///
    /// Must be called BEFORE navigating to the app (cookies must be in the jar before
    /// the first request that needs them).
    /// </summary>
    protected async Task AuthenticateContextAsync()
    {
        var secret = await SeedRefreshTokenAsync(TestUserId);

        await Context.AddCookiesAsync(new[]
        {
            new Cookie
            {
                Name = "fs_refresh",
                Value = secret,
                Domain = "localhost",
                Path = "/auth",
                HttpOnly = true,
                SameSite = SameSiteAttribute.Lax,
                Secure = false,  // test server uses http, not https
            },
        });
    }

    /// <summary>Returns true when the Blazor WASM client is available and tests can run in the browser.</summary>
    protected bool ClientAvailable => _clientServer.IsAvailable;

    /// <summary>
    /// Seeds a refresh token for <paramref name="userId"/>, then opens a new
    /// <see cref="IBrowserContext"/> with the cookie pre-set and returns its first page.
    /// Use when a test needs a second authenticated user alongside the primary one.
    /// Dispose the returned <see cref="IPage"/> (and its context) after the test.
    /// </summary>
    protected async Task<IPage> CreatePageForUserAsync(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_apiServer.DbConnectionString);
        await conn.OpenAsync();

        var secret = await SeedRefreshTokenForUserAsync(userId, conn);

        var ctx = await Context.Browser!.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = E2EConfig.ClientBaseUrl,
            IgnoreHTTPSErrors = true,
        });

        await ctx.AddCookiesAsync(new[]
        {
            new Cookie
            {
                Name     = "fs_refresh",
                Value    = secret,
                Domain   = "localhost",
                Path     = "/auth",
                HttpOnly = true,
                SameSite = SameSiteAttribute.Lax,
                Secure   = false,
            },
        });

        return await ctx.NewPageAsync();
    }

    // ── Seed helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a Family + User + linked FamilyMember into the E2E database.
    /// Returns (userId, memberId, familyId).
    /// </summary>
    private async Task<(Guid userId, Guid memberId, Guid familyId)> SeedTestUserAsync()
    {
        await using var conn = new NpgsqlConnection(_apiServer.DbConnectionString);
        await conn.OpenAsync();

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO families (id, name, created_at, updated_at)
                VALUES (@id, @name, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", "E2E Test Family");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @externalId, @provider, @email, @displayName, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("externalId", "google-e2e-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", $"e2e-{userId:N}@test.example");
            cmd.Parameters.AddWithValue("displayName", "E2E Test User");
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members
                    (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @familyId, @userId, @email, @displayName, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("familyId", familyId);
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("email", $"e2e-{userId:N}@test.example");
            cmd.Parameters.AddWithValue("displayName", "E2E Test User");
            await cmd.ExecuteNonQueryAsync();
        }

        return (userId, memberId, familyId);
    }

    /// <summary>
    /// Inserts a refresh_tokens row for <paramref name="userId"/> and returns the
    /// plaintext secret (to be stored in the fs_refresh cookie).
    /// </summary>
    /// <summary>
    /// Seeds a refresh token for any user ID and returns the plaintext secret.
    /// Flow tests can use this directly when setting up second-user auth contexts.
    /// </summary>
    protected async Task<string> SeedRefreshTokenForUserAsync(
        Guid userId, NpgsqlConnection conn)
    {
        var secret = GenerateSecret();
        var hash = ComputeSha256(secret);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO refresh_tokens
                (id, user_id, token_hash, created_at, expires_at, created_from_ip, user_agent)
            VALUES (@id, @userId, @hash, now(), @expiresAt, null, 'e2e-test-agent')
            """;
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.AddDays(30));
        await cmd.ExecuteNonQueryAsync();

        return secret;
    }

    private async Task<string> SeedRefreshTokenAsync(Guid userId)
    {
        var secret = GenerateSecret();
        var hash = ComputeSha256(secret);

        await using var conn = new NpgsqlConnection(_apiServer.DbConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO refresh_tokens
                (id, user_id, token_hash, created_at, expires_at, created_from_ip, user_agent)
            VALUES (@id, @userId, @hash, now(), @expiresAt, null, 'e2e-test-agent')
            """;
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("userId", userId);
        cmd.Parameters.AddWithValue("hash", hash);
        cmd.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.AddDays(30));
        await cmd.ExecuteNonQueryAsync();

        return secret;
    }

    // ── Playwright waiting helpers ────────────────────────────────────────────

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for any network activity to settle.
    /// Use instead of Thread.Sleep or fixed delays.
    /// </summary>
    protected static Task WaitForNetworkIdleAsync(IPage page, int timeout = 10_000) =>
        page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
        {
            Timeout = timeout,
        });

    // ── Crypto helpers ────────────────────────────────────────────────────────

    private static string GenerateSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] ComputeSha256(string value)
    {
        var hash = new byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(value), hash);
        return hash;
    }
}
