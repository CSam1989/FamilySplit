using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.Activities;

public static class ActivityReducers
{
    // ── Load List ─────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadActivitiesAction))]
    public static ActivityState OnLoad(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnLoadSuccess(ActivityState state, LoadActivitiesSuccessAction action) =>
        state with { IsLoading = false, Activities = action.Activities };

    [ReducerMethod]
    public static ActivityState OnLoadFailure(ActivityState state, LoadActivitiesFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Load Detail ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadActivityDetailAction))]
    public static ActivityState OnLoadDetail(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null, SelectedActivity = null };

    [ReducerMethod]
    public static ActivityState OnLoadDetailSuccess(ActivityState state, LoadActivityDetailSuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnLoadDetailFailure(ActivityState state, LoadActivityDetailFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CreateActivityAction))]
    public static ActivityState OnCreate(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnCreateSuccess(ActivityState state, CreateActivitySuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnCreateFailure(ActivityState state, CreateActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create Sub-Activity ───────────────────────────────────────────────────

    [ReducerMethod(typeof(CreateSubActivityAction))]
    public static ActivityState OnCreateSub(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnCreateSubSuccess(ActivityState state, CreateSubActivitySuccessAction action)
    {
        // action.Activity is the *new sub-activity*, not the parent.
        // Keep the parent as SelectedActivity and append the sub to its list.
        if (state.SelectedActivity is null)
            return state with { IsLoading = false };

        var sub = action.Activity;
        var summary = new ActivitySummaryDto(
            sub.Id, sub.GroupId, sub.Name, sub.Description, sub.Status,
            sub.ParentActivityId, sub.Participants.Count, 0,
            sub.CreatedAt, sub.ClosedAt);

        var updatedParent = state.SelectedActivity with
        {
            SubActivities = [.. state.SelectedActivity.SubActivities, summary]
        };

        return state with { IsLoading = false, SelectedActivity = updatedParent };
    }

    [ReducerMethod]
    public static ActivityState OnCreateSubFailure(ActivityState state, CreateSubActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateActivityAction))]
    public static ActivityState OnUpdate(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnUpdateSuccess(ActivityState state, UpdateActivitySuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnUpdateFailure(ActivityState state, UpdateActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Close ─────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CloseActivityAction))]
    public static ActivityState OnClose(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnCloseSuccess(ActivityState state, CloseActivitySuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnCloseFailure(ActivityState state, CloseActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Add Participant ───────────────────────────────────────────────────────

    [ReducerMethod(typeof(AddParticipantAction))]
    public static ActivityState OnAddParticipant(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnAddParticipantSuccess(ActivityState state, AddParticipantSuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnAddParticipantFailure(ActivityState state, AddParticipantFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Remove Participant ────────────────────────────────────────────────────

    [ReducerMethod(typeof(RemoveParticipantAction))]
    public static ActivityState OnRemoveParticipant(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static ActivityState OnRemoveParticipantSuccess(ActivityState state, RemoveParticipantSuccessAction action) =>
        state with { IsLoading = false, SelectedActivity = action.Activity };

    [ReducerMethod]
    public static ActivityState OnRemoveParticipantFailure(ActivityState state, RemoveParticipantFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear Error ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearActivityErrorAction))]
    public static ActivityState OnClearError(ActivityState state) =>
        state with { ErrorMessage = null };
}
