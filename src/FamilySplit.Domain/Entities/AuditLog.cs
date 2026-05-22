namespace FamilySplit.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = default!;
    /// <summary>Optional JSON blob of before/after diff.</summary>
    public string? Metadata { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public User? User { get; set; }
}
