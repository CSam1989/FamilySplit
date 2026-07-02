using FamilySplit.Common.Modules;
using FamilySplit.Common.Notifications;
using FamilySplit.Common.Routing;
using FamilySplit.Features.Notifications.Data;
using FamilySplit.Features.Notifications.GetVapidPublicKey;
using FamilySplit.Features.Notifications.Shared;
using FamilySplit.Features.Notifications.Subscribe;
using FamilySplit.Features.Notifications.Unsubscribe;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Notifications;

/// <summary>
/// The Notifications feature slice: VAPID public-key retrieval, push-subscription management, and
/// the real-time delivery mechanism (SignalR hub + composite <see cref="INotificationService"/>).
/// The delivery mechanism is not itself a command/query use case — Settlements depends on
/// <see cref="INotificationService"/> (declared in Common) without depending on this slice.
/// </summary>
public sealed class NotificationsModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(NotificationsModule).Assembly);

        services.AddScoped<IPushSubscriptionData, PushSubscriptionData>();   // the data seam (ADR-001)
        services.AddScoped<GetVapidPublicKeyQueryHandler>();
        services.AddScoped<SubscribeCommandHandler>();
        services.AddScoped<UnsubscribeCommandHandler>();

        // Real-time delivery mechanism.
        services.AddSignalR();
        services.AddScoped<VapidPushSender>();
        services.AddScoped<INotificationService, SignalRNotificationService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/push")
            .WithTags("Notifications");

        GetVapidPublicKeyEndpoint.Map(grp);
        SubscribeEndpoint.Map(grp);
        UnsubscribeEndpoint.Map(grp);

        // SignalR hub — Blazor WASM passes the JWT as ?access_token= because WebSocket upgrade
        // requests cannot carry Authorization headers (see the Auth slice's JwtBearer wiring).
        endpoints.MapHub<NotificationHub>(HubPaths.Notifications);
    }
}
