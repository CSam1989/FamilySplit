using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.List;

internal static class ListSettlementsEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/", async (Guid groupId, Guid activityId,
                               ListSettlementsQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var settlements = await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.Ok(settlements);
        });
}
