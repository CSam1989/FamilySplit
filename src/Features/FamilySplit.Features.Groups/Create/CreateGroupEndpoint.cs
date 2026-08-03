using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.Create;

internal static class CreateGroupEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/", async (CreateGroupCommand cmd,
                                CreateGroupCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/groups/{id}", new CreatedResponse(id));
        });
}
