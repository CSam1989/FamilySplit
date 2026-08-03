using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.Update;

internal static class UpdateGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/{groupId:guid}", async (Guid groupId, UpdateGroupCommand cmd,
                                             UpdateGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
