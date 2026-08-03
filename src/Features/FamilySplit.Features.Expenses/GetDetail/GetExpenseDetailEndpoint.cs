using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Expenses.GetDetail;

internal static class GetExpenseDetailEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapGet("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId,
                                                GetExpenseDetailQueryHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var detail = await handler.HandleAsync(expenseId, ctx.User.GetUserId(), ct);
            return Results.Ok(detail);
        });
}
