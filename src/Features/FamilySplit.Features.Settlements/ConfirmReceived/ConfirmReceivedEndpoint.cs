using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.ConfirmReceived;

internal static class ConfirmReceivedEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{settlementId:guid}/confirm-received", async (Guid groupId, Guid activityId, Guid settlementId,
                                                                   ConfirmReceivedCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(settlementId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
