using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Notifications.GetVapidPublicKey;

internal static class GetVapidPublicKeyEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/vapid-public-key", (GetVapidPublicKeyQueryHandler handler) =>
        {
            var key = handler.Handle();
            return Results.Ok(new { publicKey = key });
        }).AllowAnonymous();
}
