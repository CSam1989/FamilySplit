using Microsoft.JSInterop;
using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Manages push notification permission and browser subscription from Blazor WASM.
/// Calls the JS helpers in wwwroot/js/push.js via JSRuntime,
/// then persists the subscription to the API via IPushClient.
/// </summary>
public class PushNotificationClientService
{
    private readonly IJSRuntime   _js;
    private readonly IPushClient  _pushClient;
    private readonly ILogger<PushNotificationClientService> _logger;

    public PushNotificationClientService(
        IJSRuntime  js,
        IPushClient pushClient,
        ILogger<PushNotificationClientService> logger)
    {
        _js         = js;
        _pushClient = pushClient;
        _logger     = logger;
    }

    // ── Feature / permission helpers ──────────────────────────────────────────

    /// <summary>Returns false on browsers that don't support the Push API.</summary>
    public async Task<bool> IsSupportedAsync() =>
        await _js.InvokeAsync<bool>("FamilySplitPush.isSupported");

    /// <summary>Returns 'default', 'granted', 'denied', or 'unsupported'.</summary>
    public async Task<string> GetPermissionStateAsync() =>
        await _js.InvokeAsync<string>("FamilySplitPush.getPermissionState");

    // ── Subscribe ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Asks the browser for notification permission (shows the browser prompt),
    /// creates a push subscription, and registers it with the API.
    /// Returns true if the user granted permission and the subscription was saved.
    /// </summary>
    public async Task<bool> SubscribeAsync()
    {
        try
        {
            // Step 1 — request browser permission.
            var granted = await _js.InvokeAsync<bool>("FamilySplitPush.requestPermission");
            if (!granted)
            {
                _logger.LogInformation("Push permission denied by user");
                return false;
            }

            // Step 2 — fetch VAPID public key from API.
            var vapidResponse = await _pushClient.GetVapidPublicKeyAsync();

            // Step 3 — create browser subscription using VAPID key.
            var sub = await _js.InvokeAsync<BrowserSubscription?>("FamilySplitPush.subscribe", vapidResponse.PublicKey);
            if (sub is null)
            {
                _logger.LogWarning("Browser push subscription returned null");
                return false;
            }

            // Step 4 — persist subscription on the server.
            await _pushClient.SubscribeAsync(new PushSubscribeRequest(sub.Endpoint, sub.P256dh, sub.Auth));
            _logger.LogInformation("Push subscription registered successfully");
            return true;
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error registering push subscription: {Status}", ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering push subscription");
            return false;
        }
    }

    // ── Unsubscribe ───────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the browser's push subscription and tells the API to forget it.
    /// </summary>
    public async Task<bool> UnsubscribeAsync()
    {
        try
        {
            var endpoint = await _js.InvokeAsync<string?>("FamilySplitPush.unsubscribe");
            if (endpoint is null) return true; // already gone

            await _pushClient.UnsubscribeAsync(new PushUnsubscribeRequest(endpoint));
            _logger.LogInformation("Push subscription removed");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing push subscription");
            return false;
        }
    }

    // ── Check current state ───────────────────────────────────────────────────

    /// <summary>Returns true if the browser currently has an active push subscription.</summary>
    public async Task<bool> IsSubscribedAsync()
    {
        var endpoint = await _js.InvokeAsync<string?>("FamilySplitPush.getCurrentEndpoint");
        return endpoint is not null;
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private record BrowserSubscription(string Endpoint, string P256dh, string Auth);
}
