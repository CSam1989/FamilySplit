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
    // Strict CQRS: the command returns only an id; the effect re-queries the list, so the
    // success reducer just stops loading — it no longer patches state from a returned DTO.

    [ReducerMethod(typeof(CreateActivityAction))]
    public static ActivityState OnCreate(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(CreateActivitySuccessAction))]
    public static ActivityState OnCreateSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnCreateFailure(ActivityState state, CreateActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create Sub-Activity ───────────────────────────────────────────────────
    // Strict CQRS: the effect re-queries the parent's detail, which repopulates
    // SelectedActivity (and its sub list) — the success reducer just stops loading.

    [ReducerMethod(typeof(CreateSubActivityAction))]
    public static ActivityState OnCreateSub(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(CreateSubActivitySuccessAction))]
    public static ActivityState OnCreateSubSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnCreateSubFailure(ActivityState state, CreateSubActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateActivityAction))]
    public static ActivityState OnUpdate(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(UpdateActivitySuccessAction))]
    public static ActivityState OnUpdateSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnUpdateFailure(ActivityState state, UpdateActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Close ─────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CloseActivityAction))]
    public static ActivityState OnClose(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(CloseActivitySuccessAction))]
    public static ActivityState OnCloseSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnCloseFailure(ActivityState state, CloseActivityFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Add Participant ───────────────────────────────────────────────────────

    [ReducerMethod(typeof(AddParticipantAction))]
    public static ActivityState OnAddParticipant(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(AddParticipantSuccessAction))]
    public static ActivityState OnAddParticipantSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnAddParticipantFailure(ActivityState state, AddParticipantFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Remove Participant ────────────────────────────────────────────────────

    [ReducerMethod(typeof(RemoveParticipantAction))]
    public static ActivityState OnRemoveParticipant(ActivityState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(RemoveParticipantSuccessAction))]
    public static ActivityState OnRemoveParticipantSuccess(ActivityState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static ActivityState OnRemoveParticipantFailure(ActivityState state, RemoveParticipantFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear Error ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearActivityErrorAction))]
    public static ActivityState OnClearError(ActivityState state) =>
        state with { ErrorMessage = null };
}
