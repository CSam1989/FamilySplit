using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FamilySplit.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory that replaces the production AppDbContext registration
/// with one bound to a caller-supplied open NpgsqlConnection AND its ambient
/// NpgsqlTransaction. This makes every AppDbContext the API resolves during a
/// test participate in the same physical transaction, enabling test isolation via
/// a single BeginTransaction / Rollback cycle.
///
/// Key insight: passing the connection alone is not enough — EF Core's
/// SaveChangesAsync will still try to BEGIN its own transaction on the shared
/// connection. Calling context.Database.UseTransaction(...) on every new DbContext
/// tells EF Core to use the externally-managed transaction instead, so it never
/// issues its own BEGIN/COMMIT.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly NpgsqlConnection _sharedConnection;
    private readonly NpgsqlTransaction _sharedTransaction;
    private readonly string _connectionString;

    /// <param name="sharedConnection">
    ///     An open NpgsqlConnection (pooling disabled) whose transaction has already
    ///     been started. Every AppDbContext the API resolves will reuse this connection.
    /// </param>
    /// <param name="sharedTransaction">
    ///     The active NpgsqlTransaction on <paramref name="sharedConnection"/>.
    ///     Passed to every DbContext via UseTransaction so EF Core never starts its own.
    /// </param>
    /// <param name="connectionString">
    ///     Container connection string — overrides ConnectionStrings:Postgres for any
    ///     code that reads the raw string before the DI swap takes effect.
    /// </param>
    public CustomWebApplicationFactory(
        NpgsqlConnection sharedConnection,
        NpgsqlTransaction sharedTransaction,
        string connectionString)
    {
        _sharedConnection = sharedConnection;
        _sharedTransaction = sharedTransaction;
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Point any direct connection-string readers at the test container.
        builder.UseSetting("ConnectionStrings__Postgres", _connectionString);
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);

        builder.ConfigureServices(services =>
        {
            // Remove every AppDbContext-related descriptor so we can replace it.
            // This catches AddDbContextPool's internal registrations
            // (IDbContextPool<AppDbContext>, DbContextOptions<AppDbContext>, etc.)
            // as well as plain AddDbContext registrations.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Register AppDbContext as Scoped using a factory delegate so we can
            // call UseTransaction on every instance. Using AddScoped with a factory
            // (rather than AddDbContext) bypasses EF Core's internal DbContext pooling
            // and option-caching, giving us full control over each instance.
            services.AddScoped<AppDbContext>(_ =>
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(_sharedConnection, npg =>
                    {
                        npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        // Disable retry-on-failure: retries open new connections and
                        // break the single-connection transaction contract.
                        npg.EnableRetryOnFailure(0);
                    })
                    .EnableDetailedErrors()
                    .Options;

                var context = new AppDbContext(options);

                // This is the critical call: tell EF Core to use the test's ambient
                // transaction instead of starting its own. Without this, SaveChangesAsync
                // issues a BEGIN on a connection that already has an open transaction,
                // which Postgres rejects with an error (causing 500 responses).
                context.Database.UseTransaction(_sharedTransaction);

                return context;
            });
        });
    }
}
