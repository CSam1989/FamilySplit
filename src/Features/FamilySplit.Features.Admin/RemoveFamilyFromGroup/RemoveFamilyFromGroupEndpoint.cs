using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.RemoveFamilyFromGroup;

internal static class RemoveFamilyFromGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/groups/{groupId:guid}/families/{familyId:guid}", async (Guid groupId, Guid familyId,
                                                                              RemoveFamilyFromGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, familyId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
