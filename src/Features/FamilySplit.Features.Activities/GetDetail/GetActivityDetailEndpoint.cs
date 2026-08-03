using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.GetDetail;

internal static class GetActivityDetailEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/{activityId:guid}", async (Guid groupId, Guid activityId,
                                                GetActivityDetailQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var detail = await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.Ok(detail);
        });
}
