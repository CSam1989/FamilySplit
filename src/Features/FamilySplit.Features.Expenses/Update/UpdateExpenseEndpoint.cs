using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Expenses.Update;

internal static class UpdateExpenseEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId, UpdateExpenseCommand cmd,
                                               UpdateExpenseCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(expenseId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
