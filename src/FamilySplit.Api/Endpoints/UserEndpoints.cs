using System.Security.Claims;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FamilySplit.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        // -----------------------------------------------------------------------------
        // GET /whoami
        //
        // Requires a valid JWT. Returns the caller's id, email, display name, and provider.
        // Used as the canonical smoke test that the bearer-token round-trip works.
        // -----------------------------------------------------------------------------
        app.MapGet("/whoami", async (
            ClaimsPrincipal user,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = user.GetUserId();
            var row = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.DisplayName,
                    u.AvatarUrl,
                    Provider = u.Provider.ToString(),
                    u.CreatedAt,
                    u.IsGlobalAdmin
                })
                .FirstOrDefaultAsync(ct);

            return row is null ? Results.NotFound() : Results.Ok(row);
        }).RequireAuthorization();

        return app;
    }
}
