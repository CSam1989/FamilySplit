using FamilySplit.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Api.Hubs;

/// <summary>
/// SignalR hub for real-time settlement notifications.
///
/// Group naming: "family-{familyId}" — one group per family.
/// On connect the hub resolves the caller's familyId from their JWT sub claim
/// and joins them to that group automatically. All members of the same family
/// receive the same real-time events regardless of which device they're on.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(AppDbContext db, ILogger<NotificationHub> logger)
    {
        _db     = db;
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

        var familyId = await _db.FamilyMembers
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync();

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
