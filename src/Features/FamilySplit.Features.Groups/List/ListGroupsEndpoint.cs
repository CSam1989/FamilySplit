using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.List;

internal static class ListGroupsEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/", async (ListGroupsQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var groups = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return Results.Ok(groups);
        });
}
