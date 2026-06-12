namespace FamilySplit.Common.Notifications;

/// <summary>
/// Sends real-time notifications to connected family members.
/// Implementations broadcast to the SignalR group "family-{familyId}".
/// Lives in Common so slices (e.g. Settlements) can notify without depending
/// on the Notifications slice that implements delivery.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a settlement notification to all connected members of the target family.
    /// </summary>
    Task NotifyFamilyAsync(
        Guid targetFamilyId,
        string title,
        string message,
        string? url = null,
        CancellationToken ct = default);
}
