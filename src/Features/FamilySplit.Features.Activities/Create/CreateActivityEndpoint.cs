using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.Create;

internal static class CreateActivityEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/", async (Guid groupId, CreateActivityCommand cmd,
                                CreateActivityCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(groupId, cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/groups/{groupId}/activities/{id}", new CreatedResponse(id));
        });
}
