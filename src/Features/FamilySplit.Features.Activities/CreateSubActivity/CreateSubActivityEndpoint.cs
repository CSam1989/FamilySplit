using FamilySplit.Common.Contracts;
using FamilySplit.Features.Activities.Create;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.CreateSubActivity;

internal static class CreateSubActivityEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{activityId:guid}/sub-activities", async (Guid groupId, Guid activityId, CreateActivityCommand cmd,
                                                               CreateSubActivityCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(activityId, cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/groups/{groupId}/activities/{id}", new CreatedResponse(id));
        });
}
