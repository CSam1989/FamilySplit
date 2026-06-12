using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Expenses.Delete;

internal static class DeleteExpenseEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId,
                                                  DeleteExpenseCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(expenseId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
