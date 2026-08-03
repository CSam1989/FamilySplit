using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Expenses.List;

internal static class ListExpensesEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/", async (Guid groupId, Guid activityId,
                               ListExpensesQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var expenses = await handler.HandleAsync(activityId, ctx.User.GetUserId(), ct);
            return Results.Ok(expenses);
        });
}
