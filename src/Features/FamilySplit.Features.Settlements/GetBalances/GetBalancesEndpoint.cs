using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Settlements.GetBalances;

internal static class GetBalancesEndpoint
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/balances")
            .WithTags("Settlements");

        grp.MapGet("/", async (Guid groupId, Guid activityId,
                               GetBalancesQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var balances = await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.Ok(balances);
        });
    }
}
