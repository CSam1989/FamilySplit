using FamilySplit.Infrastructure;

namespace FamilySplit.Application.Settlements;

/// <summary>
/// Lands in Phase 7 (Settlements): ConfirmSent / ConfirmReceived state transitions,
/// Activity → Settled when all settlements complete.
/// </summary>
public class SettlementService
{
    private readonly AppDbContext _db;

    public SettlementService(AppDbContext db) => _db = db;

    // Methods added in Phase 7.
}
