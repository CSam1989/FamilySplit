namespace FamilySplit.Application.Notifications;

/// <summary>
/// Sends real-time notifications to connected family members.
/// Implementations broadcast to the SignalR group "family-{familyId}".
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a settlement notification to all connected members of the target family.
    /// </summary>
    Task NotifyFamilyAsync(
        Guid   targetFamilyId,
        string title,
        string message,
        string? url = null,
        CancellationToken ct = default);
}
