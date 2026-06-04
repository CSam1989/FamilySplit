using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FamilySplit.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a single postgres:16-alpine container for the entire test collection
/// and applies EF migrations once on startup. Tests share this container and
/// use per-test transaction rollback for isolation.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    // Pass the image directly to the constructor (parameterless ctor is obsolete in v4.x).
    // PostgreSqlBuilder already has built-in readiness detection — no custom wait needed.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("familysplit_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    /// <summary>The connection string for the running container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // Apply EF Core migrations to the freshly-started container.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            })
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
