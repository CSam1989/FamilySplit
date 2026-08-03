using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.UpdateFamilyName;

internal static class UpdateFamilyNameEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/", async (UpdateFamilyNameCommand cmd,
                              UpdateFamilyNameCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
