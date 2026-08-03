using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Common.Modules;

/// <summary>
/// Contract every feature slice implements exactly once. The API host holds an
/// explicit list of modules and calls <see cref="RegisterServices"/> before
/// <c>builder.Build()</c> and <see cref="MapEndpoints"/> after, so a slice owns
/// both its DI registrations and its route table.
/// </summary>
public interface IFeatureModule
{
    /// <summary>Registers the slice's handlers, validators, and options.</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the slice's minimal-API endpoints (and hubs, if any).</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
