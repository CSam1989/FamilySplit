using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.AddParticipant;

internal static class AddParticipantEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{activityId:guid}/participants", async (Guid groupId, Guid activityId, AddParticipantCommand cmd,
                                                             AddParticipantCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(activityId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
