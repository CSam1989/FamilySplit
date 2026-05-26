using Fluxor;

namespace FamilySplit.Client.Store.Dashboard;

public static class DashboardReducers
{
    [ReducerMethod(typeof(LoadDashboardStatsAction))]
    public static DashboardState OnLoad(DashboardState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static DashboardState OnLoadSuccess(DashboardState state, LoadDashboardStatsSuccessAction action) =>
        state with { IsLoading = false, Stats = action.Stats };

    [ReducerMethod]
    public static DashboardState OnLoadFailure(DashboardState state, LoadDashboardStatsFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    [ReducerMethod(typeof(ClearDashboardErrorAction))]
    public static DashboardState OnClearError(DashboardState state) =>
        state with { ErrorMessage = null };
}
