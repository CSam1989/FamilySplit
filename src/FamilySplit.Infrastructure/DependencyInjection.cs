using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFamilySplitInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing connection string 'Postgres'. " +
                "Set it via user-secrets, environment variable (ConnectionStrings__Postgres), or appsettings.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            }));

        return services;
    }
}
