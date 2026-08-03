using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Notifications.Subscribe;

internal static class SubscribeEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/subscribe", async (
            SubscribeCommand cmd,
            SubscribeCommandHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
