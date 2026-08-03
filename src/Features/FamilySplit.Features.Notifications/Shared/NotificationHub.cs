using FamilySplit.Features.Notifications.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Notifications.Shared;

/// <summary>
/// SignalR hub for real-time settlement notifications.
///
/// Group naming: "family-{familyId}" — one group per family.
/// On connect the hub resolves the caller's familyId from their JWT sub claim
/// and joins them to that group automatically. All members of the same family
/// receive the same real-time events regardless of which device they're on.
///
/// Depends on <see cref="IPushSubscriptionData"/> rather than <c>AppDbContext</c> directly
/// (ADR-001/Rule 5 — "Hub" is not a sanctioned AppDbContext-injecting suffix), which also keeps
/// the connect-resolution logic mockable/testable without a database.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly IPushSubscriptionData _data;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IPushSubscriptionData data, ILogger<NotificationHub> logger)
    {
        _data = data;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCallerId();
        if (userId is null)
        {
            await base.OnConnectedAsync();
            return;
        }

        var familyId = await _data.GetActiveFamilyIdForUserAsync(userId.Value, Context.ConnectionAborted);

        if (familyId is not null)
        {
            var group = FamilyGroup(familyId.Value);
            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            _logger.LogDebug("User {UserId} joined SignalR group {Group}", userId, group);
        }

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR automatically removes the connection from all groups on disconnect.
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Returns the canonical SignalR group name for a family.</summary>
    public static string FamilyGroup(Guid familyId) => $"family-{familyId}";

    private Guid? GetCallerId()
    {
        var sub = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
               ?? Context.User?.FindFirst("sub")?.Value;
        return sub is not null && Guid.TryParse(sub, out var id) ? id : null;
    }
}
