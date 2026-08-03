using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.ListFamilies;

internal static class ListFamiliesEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/families", async (ListFamiliesQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var families = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return Results.Ok(families);
        });
}
