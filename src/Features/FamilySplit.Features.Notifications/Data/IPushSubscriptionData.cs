namespace FamilySplit.Features.Notifications.Data;

/// <summary>
/// The Notifications slice data-access seam (ADR-001). All EF/<c>AppDbContext</c> access lives
/// behind this interface so business logic (the Subscribe/Unsubscribe command handlers) and the
/// SignalR hub hold no EF and are unit-tested by mocking it. Reads return plain records — never
/// tracked entities. <c>GetVapidPublicKey</c> reads only <c>IConfiguration</c>, so it has no
/// dependency here. The implementation is Testcontainers-tested.
/// </summary>
public interface IPushSubscriptionData
{
    // ── hub connect resolution ───────────────────────────────────────────────────

    /// <summary>The active FamilyId for a connecting user, or null if they have none (silent no-op — the hub does not throw on connect).</summary>
    Task<Guid?> GetActiveFamilyIdForUserAsync(Guid userId, CancellationToken ct);

    // ── VAPID background-push reads ──────────────────────────────────────────────

    /// <summary>Active FamilyMember.UserIds for a family (used to resolve who to push to).</summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsForFamilyAsync(Guid familyId, CancellationToken ct);

    /// <summary>Every stored push subscription for the given user ids.</summary>
    Task<IReadOnlyList<PushSubscriptionRecord>> GetSubscriptionsForUsersAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);

    /// <summary>Best-effort removal of subscriptions whose endpoint the push service reported as gone/not-found.</summary>
    Task RemoveStaleSubscriptionsAsync(IReadOnlyList<string> endpoints, CancellationToken ct);

    // ── writes ───────────────────────────────────────────────────────────────────

    /// <summary>Upserts a subscription keyed by endpoint: overwrites UserId/keys if the endpoint already exists, else inserts.</summary>
    Task UpsertSubscriptionAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct);

    /// <summary>Deletes the subscription matching both userId and endpoint, if any. Returns true if a row was removed.</summary>
    Task<bool> RemoveSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct);
}

/// <summary>A plain projection of a stored push subscription (never a tracked entity).</summary>
public sealed record PushSubscriptionRecord(Guid UserId, string Endpoint, string P256dh, string Auth);
