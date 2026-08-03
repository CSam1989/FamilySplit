using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.ConfirmSent;

internal static class ConfirmSentEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{settlementId:guid}/confirm-sent", async (Guid groupId, Guid activityId, Guid settlementId,
                                                               ConfirmSentCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(settlementId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
