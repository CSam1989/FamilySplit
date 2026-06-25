using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.AddFamilyToGroup;

internal static class AddFamilyToGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/groups/{groupId:guid}/families", async (Guid groupId, AddFamilyToGroupCommand cmd,
                                                             AddFamilyToGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
