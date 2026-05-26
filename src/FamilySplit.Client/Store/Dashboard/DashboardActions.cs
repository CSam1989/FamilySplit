using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Dashboard;

// Trigger a (re)load of dashboard statistics.
public record LoadDashboardStatsAction;

public record LoadDashboardStatsSuccessAction(IReadOnlyList<DashboardGroupStatDto> Stats);

public record LoadDashboardStatsFailureAction(string ErrorMessage);

public record ClearDashboardErrorAction;
