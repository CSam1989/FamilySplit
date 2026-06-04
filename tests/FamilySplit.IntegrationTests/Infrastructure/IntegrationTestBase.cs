using Npgsql;

namespace FamilySplit.IntegrationTests.Infrastructure;

/// <summary>
/// Abstract base for all integration tests. Each test gets:
/// <list type="bullet">
///   <item>A dedicated <see cref="NpgsqlConnection"/> (pooling disabled) with an open transaction.</item>
///   <item>A <see cref="CustomWebApplicationFactory"/> whose AppDbContext is bound to that connection.</item>
///   <item>An authenticated <see cref="HttpClient"/> with a pre-minted JWT.</item>
///   <item>Automatic rollback in <see cref="DisposeAsync"/> — every write (test-side and API-side) vanishes.</item>
/// </list>
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    // Signing key injected into the test host via appsettings override.
    // Must be ≥32 bytes (256 bits) for HMAC-SHA256.
    protected const string TestSigningKey = "integration-test-signing-key-xxxxxxxxxxxxxxxx";

    private readonly PostgresContainerFixture _fixture;
    private NpgsqlConnection _connection = null!;
    private NpgsqlTransaction _transaction = null!;

    /// <summary>
    /// Exposed so tests can create additional HTTP clients (e.g. anonymous or
    /// a second authenticated user) without spinning up another factory.
    /// </summary>
    protected internal WebApplicationFactory<Program> Factory { get; private set; } = null!;

    /// <summary>The HTTP client authenticated as the seeded test user.</summary>
    protected internal HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// The shared open connection. Use this for any direct-DB setup/assertion
    /// within a test — it participates in the same transaction so changes are
    /// visible to the API and rolled back after the test.
    /// </summary>
    protected internal NpgsqlConnection Connection => _connection;

    /// <summary>The seeded User.Id (== the JWT <c>sub</c> claim).</summary>
    protected internal Guid CallerId { get; private set; }

    /// <summary>The seeded FamilyMember.Id for the caller.</summary>
    protected internal Guid CallerMemberId { get; private set; }

    /// <summary>The seeded Family.Id for the caller's family.</summary>
    protected internal Guid CallerFamilyId { get; private set; }

    protected IntegrationTestBase(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Open a dedicated connection for this test with pooling disabled.
        // Disabling pooling ensures that BeginTransactionAsync enlists the
        // physical connection in a real transaction that the API also joins.
        _connection = new NpgsqlConnection(_fixture.ConnectionString + ";Pooling=false");
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();

        // Create the WAF — AppDbContext will reuse _connection for all requests.
        // Override the JWT signing key so JwtHelper.Mint and the JwtBearer
        // middleware agree on the secret.
        Factory = new CustomWebApplicationFactory(_connection, _transaction, _fixture.ConnectionString)
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Jwt:SigningKey", TestSigningKey);
                b.UseSetting("Jwt:Issuer", "familysplit");
                b.UseSetting("Jwt:Audience", "familysplit-client");
            });

        // Build the authenticated client with no automatic redirects.
        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });

        // Seed a Family, User, and linked FamilyMember directly via the shared connection.
        (CallerId, CallerMemberId, CallerFamilyId) = await SeedTestUserAsync();

        // Mint a JWT and attach it to every subsequent request.
        var token = JwtHelper.Mint(
            userId: CallerId,
            email: "testuser@integration.test",
            displayName: "Integration Test User",
            isGlobalAdmin: false,
            signingKey: TestSigningKey);

        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    public async ValueTask DisposeAsync()
    {
        // Rolling back wipes all writes made by this test and by the API handlers
        // that ran on the same physical connection.
        if (_transaction is not null)
            await _transaction.RollbackAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        if (Factory is not null)
            await Factory.DisposeAsync();

        if (Client is not null)
            Client.Dispose();
    }

    // -------------------------------------------------------------------------
    // Seed helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inserts a Family, a User, and a linked FamilyMember (admin) into the DB
    /// using the shared connection so they participate in the test transaction.
    /// Returns (userId, memberId, familyId).
    /// </summary>
    private async Task<(Guid userId, Guid memberId, Guid familyId)> SeedTestUserAsync()
    {
        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        // Insert family
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO families (id, name, created_at, updated_at)
                VALUES (@id, @name, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", "Integration Test Family");
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert user
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO users (id, external_id, provider, email, display_name, is_global_admin, created_at)
                VALUES (@id, @external_id, @provider, @email, @display_name, false, now())
                """;
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("external_id", "google-test-" + userId.ToString("N"));
            cmd.Parameters.AddWithValue("provider", "Google");
            cmd.Parameters.AddWithValue("email", "testuser@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Integration Test User");
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert family member linked to the user
        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, user_id, email, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @user_id, @email, @display_name, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", familyId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("email", "testuser@integration.test");
            cmd.Parameters.AddWithValue("display_name", "Integration Test User");
            await cmd.ExecuteNonQueryAsync();
        }

        return (userId, memberId, familyId);
    }

    /// <summary>
    /// Seeds an additional Family + FamilyMember (no User) for tests that need
    /// a second family in a group. Returns (familyId, memberId).
    /// </summary>
    protected internal async Task<(Guid familyId, Guid memberId)> SeedExtraFamilyAsync(
        string familyName = "Second Family",
        string memberDisplayName = "Second Member")
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO families (id, name, created_at, updated_at)
                VALUES (@id, @name, now(), now())
                """;
            cmd.Parameters.AddWithValue("id", familyId);
            cmd.Parameters.AddWithValue("name", familyName);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO family_members (id, family_id, display_name, is_admin, is_active, created_at)
                VALUES (@id, @family_id, @display_name, true, true, now())
                """;
            cmd.Parameters.AddWithValue("id", memberId);
            cmd.Parameters.AddWithValue("family_id", familyId);
            cmd.Parameters.AddWithValue("display_name", memberDisplayName);
            await cmd.ExecuteNonQueryAsync();
        }

        return (familyId, memberId);
    }
}
