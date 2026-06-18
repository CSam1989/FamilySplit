using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.Update;

internal static class UpdateActivityEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/{activityId:guid}", async (Guid groupId, Guid activityId, UpdateActivityCommand cmd,
                                               UpdateActivityCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(activityId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
