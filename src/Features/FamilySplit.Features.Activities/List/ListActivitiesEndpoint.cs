using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.List;

internal static class ListActivitiesEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/", async (Guid groupId,
                               ListActivitiesQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var activities = await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.Ok(activities);
        });
}
