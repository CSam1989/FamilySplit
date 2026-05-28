namespace FamilySplit.Domain.Entities;

/// <summary>
/// Stores a browser Web Push subscription for a user so the API can send
/// push notifications (e.g. when a settlement changes state).
/// Each browser+device combo creates its own subscription row.
/// </summary>
public class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Browser-provided push endpoint URL (unique per subscription).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>ECDH public key (base64url) from PushSubscription.getKey('p256dh').</summary>
    public string P256dh { get; set; } = "";

    /// <summary>Authentication secret (base64url) from PushSubscription.getKey('auth').</summary>
    public string Auth { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public User? User { get; set; }
}
