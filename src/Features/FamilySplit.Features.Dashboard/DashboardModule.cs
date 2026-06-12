using FamilySplit.Common.Modules;
using FamilySplit.Features.Dashboard.GetStats;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Dashboard;

/// <summary>
/// The Dashboard feature slice: per-group statistics for the authenticated caller.
/// A single read-only query — no commands, no validators.
/// </summary>
public sealed class DashboardModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<GetStatsQueryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/dashboard")
            .WithTags("Dashboard");

        GetStatsEndpoint.Map(grp);
    }
}
