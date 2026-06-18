using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.Close;

internal static class CloseActivityEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{activityId:guid}/close", async (Guid groupId, Guid activityId,
                                                       CloseActivityCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
