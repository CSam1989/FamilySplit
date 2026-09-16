using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Notifications.Data;

/// <summary>
/// The Notifications write-side data gateway (ADR-001): the only place in the slice that touches
/// <c>AppDbContext</c> and calls <c>SaveChangesAsync</c>. Reads return plain values/records; writes
/// accept the caller's computed state and persist it. Push-subscription mutations are non-financial,
/// so there is no audit flush here (CLAUDE.md). Verified with Testcontainers.
/// </summary>
internal sealed class PushSubscriptionData : IPushSubscriptionData
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushSubscriptionData> _logger;

    public PushSubscriptionData(AppDbContext db, ILogger<PushSubscriptionData> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid?> GetActiveFamilyIdForUserAsync(Guid userId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive)
            .Select(m => (Guid?)m.FamilyId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsForFamilyAsync(Guid familyId, CancellationToken ct) =>
        await _db.FamilyMembers
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.IsActive && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PushSubscriptionRecord>> GetSubscriptionsForUsersAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return [];

        return await _db.PushSubscriptions
            .AsNoTracking()
            .Where(ps => userIds.Contains(ps.UserId))
            .Select(ps => new PushSubscriptionRecord(ps.UserId, ps.Endpoint, ps.P256dh, ps.Auth))
            .ToListAsync(ct);
    }

    public async Task RemoveStaleSubscriptionsAsync(IReadOnlyList<string> endpoints, CancellationToken ct)
    {
        if (endpoints.Count == 0)
            return;

        var toRemove = await _db.PushSubscriptions
            .Where(ps => endpoints.Contains(ps.Endpoint))
            .ToListAsync(ct);

        _db.PushSubscriptions.RemoveRange(toRemove);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertSubscriptionAsync(Guid userId, string endpoint, string p256dh, string auth, CancellationToken ct)
    {
        var existing = await _db.PushSubscriptions
            .Where(ps => ps.Endpoint == endpoint)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
        }
        else
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct)
    {
        var row = await _db.PushSubscriptions
            .Where(ps => ps.UserId == userId && ps.Endpoint == endpoint)
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            _logger.LogDebug("RemoveSubscription no-op — no push subscription for user {UserId} at that endpoint", userId);
            return false;
        }

        _db.PushSubscriptions.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
