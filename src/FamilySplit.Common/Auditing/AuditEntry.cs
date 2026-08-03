namespace FamilySplit.Common.Auditing;

/// <summary>
/// A queued audit-log record. Command handlers (business logic) own <em>what</em> to audit
/// and build this record; the slice's data gateway passes it to <see cref="AuditService.Queue(AuditEntry)"/>
/// so the row is persisted atomically with the mutation (ADR-001). Keeps <see cref="AuditService"/>
/// — and therefore <c>AppDbContext</c> — out of the command handler.
/// </summary>
/// <param name="UserId">The acting user (JWT <c>sub</c>), or null for system actions.</param>
/// <param name="EntityType">Audited entity type — e.g. <c>Expense</c>, <c>Settlement</c>.</param>
/// <param name="EntityId">The audited entity's id.</param>
/// <param name="Action">The action — e.g. <c>Created</c>, <c>Updated</c>, <c>Deleted</c>.</param>
/// <param name="Metadata">Optional object serialised to the JSONB <c>metadata</c> column.</param>
public sealed record AuditEntry(
    Guid? UserId,
    string EntityType,
    Guid EntityId,
    string Action,
    object? Metadata = null);
