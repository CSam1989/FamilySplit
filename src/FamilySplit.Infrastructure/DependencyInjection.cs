using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FamilySplit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFamilySplitInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing connection string 'Postgres'. " +
                "Set it via user-secrets, environment variable (ConnectionStrings__Postgres), or appsettings.");

        // DbContextPool reuses DbContext instances across requests — saves the
        // allocation + initial state setup of the change tracker on every call.
        // Default pool size of 1024 is plenty for this workload.
        services.AddDbContextPool<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // Retry transient Postgres errors (connection drops, timeouts)
                // without leaking them to the caller. 3 tries within 5 seconds.
                npg.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });

            if (environment is null || environment.IsProduction())
            {
                // These checks are costly per-query and only useful while debugging.
                options.EnableThreadSafetyChecks(false);
                options.EnableDetailedErrors(false);
                options.EnableSensitiveDataLogging(false);
            }
            else
            {
                // Dev: surface schema / query problems early.
                options.EnableDetailedErrors();
            }
        });

        return services;
    }
}
