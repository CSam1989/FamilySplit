using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.GetMyProfile;

internal static class GetMyProfileEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/profile", async (GetMyProfileQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var profile = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });
}
