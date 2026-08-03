using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.Join;

internal static class JoinGroupEndpoint
{
    // Documented strict-CQRS exception: 200 (not 201) + { id } — the client only has the invite
    // code and needs the group id to navigate. No Location header (the family joined an existing
    // resource rather than creating one).
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/join", async (JoinGroupCommand cmd,
                                    JoinGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.Ok(new CreatedResponse(id));
        });
}
