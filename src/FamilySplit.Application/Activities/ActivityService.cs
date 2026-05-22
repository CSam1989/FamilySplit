using FamilySplit.Infrastructure;

namespace FamilySplit.Application.Activities;

/// <summary>
/// Lands in Phase 4 (Activities): create activity / sub-activity (depth-1 guard),
/// participant management, close flow (parent absorbs open subs).
/// </summary>
public class ActivityService
{
    private readonly AppDbContext _db;

    public ActivityService(AppDbContext db) => _db = db;

    // Methods added in Phase 4.
}
