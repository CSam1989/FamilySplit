using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Dashboard.GetStats;

internal static class GetStatsEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/stats", async (GetStatsQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var stats = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return Results.Ok(stats);
        });
}
