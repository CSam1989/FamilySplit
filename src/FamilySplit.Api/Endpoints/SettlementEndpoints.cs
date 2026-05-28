using FamilySplit.Application.Settlements;

namespace FamilySplit.Api.Endpoints;

public static class SettlementEndpoints
{
    public static WebApplication MapSettlementEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/settlements")
            .WithTags("Settlements");

        // GET  /groups/{groupId}/activities/{activityId}/settlements
        grp.MapGet("/", async (Guid groupId, Guid activityId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var settlements = await svc.ListAsync(activityId, callerId, ct);
            return Results.Ok(settlements);
        });

        // POST /groups/{groupId}/activities/{activityId}/settlements
        // Generates (or returns existing) settlements for a closed activity.
        grp.MapPost("/", async (Guid groupId, Guid activityId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var settlements = await svc.GenerateAsync(activityId, callerId, ct);
            return Results.Ok(settlements);
        });

        // GET  /groups/{groupId}/activities/{activityId}/settlements/{settlementId}
        grp.MapGet("/{settlementId:guid}", async (Guid groupId, Guid activityId, Guid settlementId,
            SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.GetDetailAsync(settlementId, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-sent
        grp.MapPost("/{settlementId:guid}/confirm-sent", async (Guid groupId, Guid activityId,
            Guid settlementId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.ConfirmSentAsync(settlementId, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-received
        grp.MapPost("/{settlementId:guid}/confirm-received", async (Guid groupId, Guid activityId,
            Guid settlementId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.ConfirmReceivedAsync(settlementId, callerId, ct);
            return Results.Ok(detail);
        });

        // GET  /settlements/pending — active settlements across ALL the caller's groups (dashboard view)
        app.MapGet("/settlements/pending",
            async (SettlementService svc, HttpContext ctx, CancellationToken ct) =>
            {
                var callerId = ctx.User.GetUserId();
                var result   = await svc.ListMyPendingAsync(callerId, ct);
                return Results.Ok(result);
            })
            .WithTags("Settlements");

        // GET  /groups/{groupId}/settlements — active (non-completed) settlements across all activities
        app.MapGet("/groups/{groupId:guid}/settlements",
            async (Guid groupId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
            {
                var callerId = ctx.User.GetUserId();
                var result   = await svc.ListForGroupAsync(groupId, callerId, ct);
                return Results.Ok(result);
            })
            .WithTags("Settlements");

        // GET  /groups/{groupId}/activities/{activityId}/balances
        // Pre-settlement per-family balance view (does not create any rows).
        var balGrp = app.MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/balances")
            .WithTags("Settlements");

        balGrp.MapGet("/", async (Guid groupId, Guid activityId, SettlementService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var balances = await svc.GetBalancesAsync(activityId, callerId, ct);
            return Results.Ok(balances);
        });

        return app;
    }
}
