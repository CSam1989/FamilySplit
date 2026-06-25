using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.DeleteGroup;

internal static class DeleteGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/groups/{groupId:guid}", async (Guid groupId,
                                                       DeleteGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
