using System.Text.Json;
using System.Text.Json.Serialization;
using FamilySplit.Domain.Entities;
using FamilySplit.Infrastructure;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Application.Audit;

/// <summary>
/// Queues <see cref="AuditLog"/> rows into the current <see cref="AppDbContext"/>
/// unit-of-work.  The entry is persisted atomically when the calling service
/// invokes <c>SaveChangesAsync()</c> — there is no separate database round-trip.
///
/// Supported entity types: <c>Expense</c>, <c>Settlement</c>.
/// Supported actions:
///   Expense  → Created | Updated | Deleted
///   Settlement → Generated | ConfirmSent | ConfirmReceived
/// </summary>
public class AuditService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Queues an audit entry.  <paramref name="metadata"/> is any object that will
    /// be serialised as JSON into the <c>metadata</c> JSONB column (null is fine).
    /// Call <c>SaveChangesAsync()</c> on the same DbContext to persist.
    /// </summary>
    public void Queue(Guid? userId, string entityType, Guid entityId, string action, object? metadata = null)
    {
        var entry = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOpts),
            Timestamp = DateTimeOffset.UtcNow,
        };

        _db.AuditLogs.Add(entry);

        _logger.LogDebug(
            "Audit queued: {AuditAction} {AuditEntityType} {AuditEntityId} by user {AuditUserId}",
            action, entityType, entityId, userId);
    }
}
