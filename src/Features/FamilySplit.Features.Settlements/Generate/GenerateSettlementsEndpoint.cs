using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.Generate;

internal static class GenerateSettlementsEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/", async (Guid groupId, Guid activityId,
                                GenerateSettlementsCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
