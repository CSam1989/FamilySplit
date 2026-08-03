using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.Leave;

internal static class LeaveGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/{groupId:guid}/leave", async (Guid groupId,
                                                      LeaveGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
