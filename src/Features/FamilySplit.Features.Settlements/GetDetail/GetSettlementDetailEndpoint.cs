using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.GetDetail;

internal static class GetSettlementDetailEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/{settlementId:guid}", async (Guid groupId, Guid activityId, Guid settlementId,
                                                  GetSettlementDetailQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var detail = await handler.HandleAsync(settlementId, ctx.User.GetUserId(), ct);
            return Results.Ok(detail);
        });
}
