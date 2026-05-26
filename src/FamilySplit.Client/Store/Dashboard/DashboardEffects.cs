using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Client.Store.Dashboard;

public class DashboardEffects
{
    private readonly IDashboardClient _client;
    private readonly ILogger<DashboardEffects> _logger;

    public DashboardEffects(IDashboardClient client, ILogger<DashboardEffects> logger)
    {
        _client = client;
        _logger = logger;
    }

    [EffectMethod(typeof(LoadDashboardStatsAction))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        try
        {
            var stats = await _client.GetStatsAsync();
            dispatcher.Dispatch(new LoadDashboardStatsSuccessAction(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard stats");
            dispatcher.Dispatch(new LoadDashboardStatsFailureAction(ErrorHelper.GetMessage(ex)));
        }
    }
}
