using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Users.WhoAmI;

/// <summary>
/// Query — returns the authenticated caller's own <c>User</c> row. Pure data access: a single
/// <c>AsNoTracking</c> projection keyed on the caller's id, no mutation, no business rules.
/// Returns <c>null</c> when no matching user exists (the endpoint maps that to 404).
/// </summary>
public sealed class WhoAmIQueryHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<WhoAmIQueryHandler> _logger;

    public WhoAmIQueryHandler(AppDbContext db, ILogger<WhoAmIQueryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WhoAmIDto?> HandleAsync(Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("WhoAmI lookup for user {UserId}", callerId);

        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerId)
            .Select(u => new WhoAmIDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.AvatarUrl,
                u.Provider.ToString(),
                u.CreatedAt,
                u.IsGlobalAdmin))
            .FirstOrDefaultAsync(ct);
    }
}
