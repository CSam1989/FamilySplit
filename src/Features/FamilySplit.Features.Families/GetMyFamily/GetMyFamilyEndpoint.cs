using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.GetMyFamily;

internal static class GetMyFamilyEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/", async (GetMyFamilyQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var family = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return family is null ? Results.NotFound() : Results.Ok(family);
        });
}
