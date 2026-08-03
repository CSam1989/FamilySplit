using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Activities.RemoveParticipant;

internal static class RemoveParticipantEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/{activityId:guid}/participants/{memberId:guid}", async (Guid groupId, Guid activityId, Guid memberId,
                                                                               RemoveParticipantCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(activityId, memberId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
