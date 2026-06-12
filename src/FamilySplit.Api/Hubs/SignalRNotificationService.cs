using FamilySplit.Application.Notifications;
using FamilySplit.Application.Push;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Api.Hubs;

/// <summary>
/// Composite INotificationService that delivers via two channels:
///
///   1. SignalR  — instant in-app delivery to every connected device of the family.
///                 The client shows a MudSnackbar toast.
///
///   2. VAPID    — Web Push delivery to subscribed browsers.
///                 The service worker receives the push event and decides whether
///                 to show an OS notification: if any app window is currently
///                 visible it suppresses the OS popup (SignalR already handled it);
///                 otherwise it shows the native notification.
///
/// Both channels fire for every event. There is no server-side coordination needed
/// because suppression is handled on the client/service-worker side.
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub> hub,
        IServiceScopeFactory scopeFactory,
        ILogger<SignalRNotificationService> logger)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task NotifyFamilyAsync(
        Guid targetFamilyId,
        string title,
        string message,
        string? url = null,
        CancellationToken ct = default)
    {
        var group = NotificationHub.FamilyGroup(targetFamilyId);

        // ── 1. SignalR (in-app, real-time) ────────────────────────────────────
        try
        {
            await _hub.Clients.Group(group).SendAsync(
                "ReceiveNotification",
                new { title, message, url = url ?? "/" },
                ct);

            _logger.LogDebug(
                "SignalR notification sent to group {Group}: {Title}", group, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SignalR delivery failed for family group {Group}", group);
        }

        // ── 2. VAPID (background, OS notification) ────────────────────────────
        // Delivered on a fresh DI scope so the background work never touches the
        // caller's request-scoped (pooled) DbContext after the request completes.
        // CancellationToken is intentionally NOT propagated — the request's token
        // would cancel delivery the moment the HTTP response is sent.
        _ = DeliverPushAsync(targetFamilyId, title, message, url);
    }

    private async Task DeliverPushAsync(Guid targetFamilyId, string title, string message, string? url)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var vapid = scope.ServiceProvider.GetRequiredService<PushNotificationService>();
            await vapid.SendToFamilyAsync(targetFamilyId, title, message, url, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background VAPID delivery failed for family {FamilyId}", targetFamilyId);
        }
    }
}
