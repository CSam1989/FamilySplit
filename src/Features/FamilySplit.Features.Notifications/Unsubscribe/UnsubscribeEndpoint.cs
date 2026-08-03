using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Notifications.Unsubscribe;

internal static class UnsubscribeEndpoint
{
    // DELETE (unlike POST/PUT/PATCH) does not infer a complex-type parameter as the request body
    // by default — [FromBody] must be explicit here, matching the legacy PushEndpoints.cs.
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/unsubscribe", async (
            [FromBody] UnsubscribeCommand cmd,
            UnsubscribeCommandHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
