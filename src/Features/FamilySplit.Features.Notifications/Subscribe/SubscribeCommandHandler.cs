using FamilySplit.Features.Notifications.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Notifications.Subscribe;

/// <summary>
/// Command (business logic) — saves/updates a browser's push subscription. Holds no EF — all data
/// access goes through <see cref="IPushSubscriptionData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class SubscribeCommandHandler
{
    private readonly IPushSubscriptionData _data;
    private readonly SubscribeCommandValidator _validator;
    private readonly ILogger<SubscribeCommandHandler> _logger;

    public SubscribeCommandHandler(
        IPushSubscriptionData data,
        SubscribeCommandValidator validator,
        ILogger<SubscribeCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task HandleAsync(SubscribeCommand cmd, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Saving push subscription for user {UserId}", callerId);

        await _validator.ValidateAndThrowAsync(cmd, ct);

        await _data.UpsertSubscriptionAsync(callerId, cmd.Endpoint, cmd.P256dh, cmd.Auth, ct);

        _logger.LogInformation("Push subscription saved for user {UserId}", callerId);
    }
}
