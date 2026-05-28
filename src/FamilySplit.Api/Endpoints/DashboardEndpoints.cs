using FamilySplit.Application.Dashboard;
using System.Security.Claims;

namespace FamilySplit.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dashboard");

        // GET /dashboard/stats
        // Returns per-group statistics for the authenticated caller's dashboard.
        group.MapGet("/stats", async (
            DashboardService svc,
            ClaimsPrincipal user) =>
        {
            var callerId = user.GetUserId();
            var stats = await svc.GetStatsAsync(callerId);
            return Results.Ok(stats);
        });
    }
}
