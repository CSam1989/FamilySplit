using Fluxor;

namespace FamilySplit.Client.Store.App;

public static class AppReducers
{
    [ReducerMethod]
    public static AppState OnSetLastUsedActivity(AppState state, SetLastUsedActivityAction action) =>
        state with
        {
            LastUsedGroupId      = action.GroupId,
            LastUsedActivityId   = action.ActivityId,
            LastUsedActivityName = action.ActivityName,
            LastUsedGroupName    = action.GroupName,
        };
}
