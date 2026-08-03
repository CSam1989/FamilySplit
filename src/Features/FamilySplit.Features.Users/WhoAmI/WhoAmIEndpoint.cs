using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Users.WhoAmI;

internal static class WhoAmIEndpoint
{
    internal static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/whoami", async (WhoAmIQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var me = await handler.HandleAsync(ctx.User.GetUserId(), ct);
            return me is null ? Results.NotFound() : Results.Ok(me);
        }).WithTags("Users");
}
