using FamilySplit.Features.Notifications.Data;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Notifications.Unsubscribe;

/// <summary>
/// Command (business logic) — removes a browser's push subscription. Holds no EF — all data access
/// goes through <see cref="IPushSubscriptionData"/> (ADR-001). No validator: matches the legacy
/// <c>PushNotificationService.UnsubscribeAsync</c> behaviour, which applied no field validation on
/// this path (unlike Subscribe). Returns nothing (204).
/// </summary>
public sealed class UnsubscribeCommandHandler
{
    private readonly IPushSubscriptionData _data;
    private readonly ILogger<UnsubscribeCommandHandler> _logger;

    public UnsubscribeCommandHandler(IPushSubscriptionData data, ILogger<UnsubscribeCommandHandler> logger)
    {
        _data = data;
        _logger = logger;
    }

    public async Task HandleAsync(UnsubscribeCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Removing push subscription for user {UserId}", callerId);

        var removed = await _data.RemoveSubscriptionAsync(callerId, cmd.Endpoint, ct);

        if (removed)
            _logger.LogInformation("Push subscription removed for user {UserId}", callerId);
    }
}
