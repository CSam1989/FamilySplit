using FamilySplit.Common.Modules;
using FamilySplit.Features.Users.WhoAmI;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Users;

/// <summary>
/// The Users feature slice: the authenticated caller's identity (<c>GET /whoami</c>).
/// A single read-only query — no commands, no validators.
/// </summary>
public sealed class UsersModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<WhoAmIQueryHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // /whoami is a top-level route with no shared prefix, so it maps directly.
        WhoAmIEndpoint.Map(endpoints);
    }
}
