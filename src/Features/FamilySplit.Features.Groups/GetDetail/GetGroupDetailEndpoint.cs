using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.GetDetail;

internal static class GetGroupDetailEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/{groupId:guid}", async (Guid groupId,
                                             GetGroupDetailQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var detail = await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.Ok(detail);
        });
}
