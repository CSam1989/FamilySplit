using System.Text.Json;
using FamilySplit.Features.Notifications.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
// Alias avoids ambiguity with FamilySplit.Domain.Entities.PushSubscription
using LibPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace FamilySplit.Features.Notifications.Shared;

/// <summary>
/// Sends Web Push (VAPID) notifications to subscribed browsers. Used as the background-delivery
/// channel: fires when the app is closed or backgrounded. The service worker decides whether to
/// surface the OS notification based on whether any app windows are visible.
///
/// Renamed from the legacy <c>PushNotificationService</c> (which also held the VAPID-public-key
/// read and the Subscribe/Unsubscribe persistence — those moved to <c>GetVapidPublicKeyQueryHandler</c>
/// and the Subscribe/Unsubscribe command handlers respectively). Depends on
/// <see cref="IPushSubscriptionData"/> rather than <c>AppDbContext</c> directly (ADR-001) so it stays
/// out of the data-access allow-list and can be resolved from a background DI scope cleanly.
/// </summary>
public class VapidPushSender
{
    private readonly IPushSubscriptionData _data;
    private readonly IConfiguration _config;
    private readonly ILogger<VapidPushSender> _logger;

    public VapidPushSender(
        IPushSubscriptionData data,
        IConfiguration config,
        ILogger<VapidPushSender> logger)
    {
        _data = data;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Sends a VAPID push notification to every subscribed browser of every
    /// active member of the given family. Best-effort; stale subscriptions
    /// (HTTP 410/404) are pruned automatically.
    /// </summary>
    public async Task SendToFamilyAsync(
        Guid familyId,
        string title,
        string body,
        string? url = null,
        CancellationToken ct = default)
    {
        var publicKey = _config["Push:Vapid:PublicKey"];
        var privateKey = _config["Push:Vapid:PrivateKey"];
        var subject = _config["Push:Vapid:Subject"] ?? "mailto:noreply@familysplit.app";

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
        {
            _logger.LogWarning("VAPID keys not configured — skipping push delivery for family {FamilyId}", familyId);
            return;
        }

        // Resolve all active user IDs for this family.
        var userIds = await _data.GetActiveUserIdsForFamilyAsync(familyId, ct);

        if (userIds.Count == 0) return;

        var subscriptions = await _data.GetSubscriptionsForUsersAsync(userIds, ct);

        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url = url ?? "/",
            tag = "familysplit-settlement",
            icon = "/icons/icon-192.png",
            badge = "/icons/icon-192.png",
        });

        // PushServiceClient is not IDisposable — do not wrap in using.
        var vapidAuth = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
        var client = new PushServiceClient { DefaultAuthentication = vapidAuth };

        var staleEndpoints = new List<string>();

        foreach (var sub in subscriptions)
        {
            try
            {
                // PushEncryptionKeyName is an enum; use SetKey() rather than dictionary indexer.
                var subscription = new LibPushSubscription();
                subscription.Endpoint = sub.Endpoint;
                subscription.SetKey(PushEncryptionKeyName.Auth, sub.Auth);
                subscription.SetKey(PushEncryptionKeyName.P256DH, sub.P256dh);

                var message = new PushMessage(payload)
                {
                    Topic = "familysplit-settlement",
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
            await _data.RemoveStaleSubscriptionsAsync(staleEndpoints, ct);
        }
    }
}
