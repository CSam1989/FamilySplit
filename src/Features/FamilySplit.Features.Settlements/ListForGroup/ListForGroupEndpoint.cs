using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.ListForGroup;

internal static class ListForGroupEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/groups/{groupId:guid}/settlements", async (Guid groupId,
                          ListForGroupQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var settlements = await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.Ok(settlements);
        }).WithTags("Settlements");
}
