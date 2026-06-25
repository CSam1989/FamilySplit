using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.GetFamily;

internal static class GetFamilyEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/families/{familyId:guid}", async (Guid familyId,
                                                       GetFamilyQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var family = await handler.HandleAsync(familyId, ctx.User.GetUserId(), ct);
            return Results.Ok(family);
        });
}
