using FamilySplit.Infrastructure;

namespace FamilySplit.Application.Expenses;

/// <summary>
/// Lands in Phase 5+ (Expenses): create / edit / dispute, ExpenseParticipant
/// seeding, SplitCalculator integration with weight snapshots.
/// </summary>
public class ExpenseService
{
    private readonly AppDbContext _db;

    public ExpenseService(AppDbContext db) => _db = db;

    // Methods added in Phase 5–6.
}
