using FamilySplit.Application.Push;
using Microsoft.AspNetCore.Mvc;

namespace FamilySplit.Api.Endpoints;

public static class PushEndpoints
{
    public static void MapPushEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/push");

        // ── GET /push/vapid-public-key ─────────────────────────────────────
        // Anonymous — the client needs this before it can subscribe.
        group.MapGet("/vapid-public-key", (PushNotificationService svc) =>
        {
            var key = svc.GetVapidPublicKey();
            return Results.Ok(new { publicKey = key });
        }).AllowAnonymous();

        // ── POST /push/subscribe ───────────────────────────────────────────
        group.MapPost("/subscribe", async (
            [FromBody] SubscribeRequest req,
            HttpContext ctx,
            PushNotificationService svc,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId();
            await svc.SubscribeAsync(userId, req.Endpoint, req.P256dh, req.Auth, ct);
            return Results.NoContent();
        });

        // ── DELETE /push/unsubscribe ───────────────────────────────────────
        group.MapDelete("/unsubscribe", async (
            [FromBody] UnsubscribeRequest req,
            HttpContext ctx,
            PushNotificationService svc,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId();
            await svc.UnsubscribeAsync(userId, req.Endpoint, ct);
            return Results.NoContent();
        });
    }

    public record SubscribeRequest(string Endpoint, string P256dh, string Auth);
    public record UnsubscribeRequest(string Endpoint);
}
