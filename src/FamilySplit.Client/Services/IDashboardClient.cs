using Refit;

namespace FamilySplit.Client.Services;

[Headers("Accept: application/json")]
public interface IDashboardClient
{
    [Get("/dashboard/stats")]
    Task<List<DashboardGroupStatDto>> GetStatsAsync();
}
