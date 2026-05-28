using FamilySplit.Application.Expenses;
using FamilySplit.Application.Expenses.Dtos;

namespace FamilySplit.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static WebApplication MapExpenseEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/expenses")
            .WithTags("Expenses");

        // GET /groups/{groupId}/activities/{activityId}/expenses
        grp.MapGet("/", async (Guid groupId, Guid activityId, ExpenseService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var expenses = await svc.ListAsync(activityId, callerId);
            return Results.Ok(expenses);
        });

        // POST /groups/{groupId}/activities/{activityId}/expenses
        grp.MapPost("/", async (Guid groupId, Guid activityId, CreateExpenseRequest req, ExpenseService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.CreateAsync(activityId, req, callerId);
            return Results.Created(
                $"/groups/{groupId}/activities/{activityId}/expenses/{detail.Id}", detail);
        });

        // GET /groups/{groupId}/activities/{activityId}/expenses/{expenseId}
        grp.MapGet("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId, ExpenseService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.GetDetailAsync(expenseId, callerId);
            return Results.Ok(detail);
        });

        // PUT /groups/{groupId}/activities/{activityId}/expenses/{expenseId}
        grp.MapPut("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId, UpdateExpenseRequest req, ExpenseService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.UpdateAsync(expenseId, req, callerId);
            return Results.Ok(detail);
        });

        // DELETE /groups/{groupId}/activities/{activityId}/expenses/{expenseId}
        grp.MapDelete("/{expenseId:guid}", async (Guid groupId, Guid activityId, Guid expenseId, ExpenseService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            await svc.DeleteAsync(expenseId, callerId);
            return Results.NoContent();
        });

        return app;
    }
}
