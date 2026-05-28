using FamilySplit.Infrastructure;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

// Alias avoids ambiguity with FamilySplit.Domain.Entities.PushSubscription
using LibPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace FamilySplit.Application.Push;

/// <summary>
/// Sends Web Push (VAPID) notifications to subscribed browsers.
/// Used as the background-delivery channel: fires when the app is closed
/// or backgrounded. The service worker decides whether to surface the OS
/// notification based on whether any app windows are visible.
/// </summary>
public class PushNotificationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        AppDbContext db,
        IConfiguration config,
        ILogger<PushNotificationService> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    // ── VAPID public key ──────────────────────────────────────────────────────

    public string GetVapidPublicKey()
    {
        var key = _config["Push:Vapid:PublicKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Push:Vapid:PublicKey is not configured. " +
                "Generate VAPID keys and store them in user secrets / env vars.");
        return key;
    }

    // ── Subscribe / unsubscribe ───────────────────────────────────────────────

    public async Task SubscribeAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct = default)
    {
        _logger.LogDebug("Saving push subscription for user {UserId}", userId);

        var existing = await _db.PushSubscriptions
            .Where(ps => ps.Endpoint == endpoint)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth   = auth;
        }
        else
        {
            _db.PushSubscriptions.Add(new Domain.Entities.PushSubscription
            {
                Id        = Guid.NewGuid(),
                UserId    = userId,
                Endpoint  = endpoint,
                P256dh    = p256dh,
                Auth      = auth,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Push subscription saved for user {UserId}", userId);
    }

    public async Task UnsubscribeAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        var row = await _db.PushSubscriptions
            .Where(ps => ps.UserId == userId && ps.Endpoint == endpoint)
            .FirstOrDefaultAsync(ct);

        if (row is not null)
        {
            _db.PushSubscriptions.Remove(row);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Push subscription removed for user {UserId}", userId);
        }
    }

    // ── Send to a whole family ────────────────────────────────────────────────

    /// <summary>
    /// Sends a VAPID push notification to every subscribed browser of every
    /// active member of the given family. Best-effort; stale subscriptions
    /// (HTTP 410/404) are pruned automatically.
    /// </summary>
    public async Task SendToFamilyAsync(
        Guid   familyId,
        string title,
        string body,
        string? url = null,
        CancellationToken ct = default)
    {
        var publicKey  = _config["Push:Vapid:PublicKey"];
        var privateKey = _config["Push:Vapid:PrivateKey"];
        var subject    = _config["Push:Vapid:Subject"] ?? "mailto:noreply@familysplit.app";

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            _logger.LogWarning("VAPID keys not configured — skipping push delivery for family {FamilyId}", familyId);
            return;
        }

        // Resolve all active user IDs for this family.
        var userIds = await _db.FamilyMembers
            .Where(m => m.FamilyId == familyId && m.IsActive && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .ToListAsync(ct);

        if (userIds.Count == 0) return;

        var subscriptions = await _db.PushSubscriptions
            .Where(ps => userIds.Contains(ps.UserId))
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url   = url ?? "/",
            tag   = "familysplit-settlement",
            icon  = "/icons/icon-192.png",
            badge = "/icons/icon-192.png",
        });

        // PushServiceClient is not IDisposable — do not wrap in using.
        var vapidAuth = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
        var client    = new PushServiceClient { DefaultAuthentication = vapidAuth };

        var staleEndpoints = new List<string>();

        foreach (var sub in subscriptions)
        {
            try
            {
                // PushEncryptionKeyName is an enum; use SetKey() rather than dictionary indexer.
                var subscription = new LibPushSubscription();
                subscription.Endpoint = sub.Endpoint;
                subscription.SetKey(PushEncryptionKeyName.Auth,   sub.Auth);
                subscription.SetKey(PushEncryptionKeyName.P256DH, sub.P256dh);

                var message = new PushMessage(payload)
                {
                    Topic      = "familysplit-settlement",
                    TimeToLive = 0, // deliver now or discard
                };

                await client.RequestPushMessageDeliveryAsync(subscription, message, ct);

                _logger.LogDebug("VAPID notification sent to user {UserId}", sub.UserId);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription has expired or been unregistered by the browser.
                _logger.LogInformation(
                    "Push subscription for user {UserId} is stale ({Status}) — removing",
                    sub.UserId, ex.StatusCode);
                staleEndpoints.Add(sub.Endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VAPID delivery failed for user {UserId}", sub.UserId);
            }
        }

        if (staleEndpoints.Count > 0)
        {
            var toRemove = await _db.PushSubscriptions
                .Where(ps => staleEndpoints.Contains(ps.Endpoint))
                .ToListAsync(ct);
            _db.PushSubscriptions.RemoveRange(toRemove);
            await _db.SaveChangesAsync(ct);
        }
    }
}
