using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Expenses.Create;

internal static class CreateExpenseEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/", async (Guid groupId, Guid activityId, CreateExpenseCommand cmd,
                                CreateExpenseCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(activityId, cmd, ctx.User.GetUserId(), ct);
            return Results.Created(
                $"/groups/{groupId}/activities/{activityId}/expenses/{id}",
                new CreatedResponse(id));
        });
}
