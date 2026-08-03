using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.ListMyPending;

internal static class ListMyPendingEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/settlements/pending", async (
                          ListMyPendingQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var settlements = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return Results.Ok(settlements);
        }).WithTags("Settlements");
}
