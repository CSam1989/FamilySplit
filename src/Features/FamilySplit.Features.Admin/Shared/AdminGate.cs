using FamilySplit.Common.Exceptions;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.Shared;

/// <summary>
/// The global-admin authorization gate for the query (read) side. The query handlers are data access
/// (they hold <c>AppDbContext</c>), so they call this static helper directly rather than a ctor-injected
/// guard. Command handlers use the mockable <c>IAdminData.IsGlobalAdminAsync</c> seam instead (ADR-001).
/// </summary>
internal static class AdminGate
{
    /// <summary>Throws <see cref="ForbiddenException"/> unless the caller's User row is a global admin.</summary>
    public static async Task RequireGlobalAdminAsync(AppDbContext db, Guid callerId, ILogger logger, CancellationToken ct)
    {
        var isAdmin = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerId)
            .Select(u => u.IsGlobalAdmin)
            .FirstOrDefaultAsync(ct);

        if (!isAdmin)
        {
            logger.LogWarning("Non-admin user {UserId} attempted a global-admin query", callerId);
            throw new ForbiddenException();
        }
    }
}
