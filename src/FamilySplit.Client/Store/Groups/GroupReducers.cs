using Fluxor;

namespace FamilySplit.Client.Store.Groups;

public static class GroupReducers
{
    // ── Load List ─────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadGroupsAction))]
    public static GroupState OnLoad(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnLoadSuccess(GroupState state, LoadGroupsSuccessAction action) =>
        state with { IsLoading = false, Groups = action.Groups };

    [ReducerMethod]
    public static GroupState OnLoadFailure(GroupState state, LoadGroupsFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Load Detail ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadGroupDetailAction))]
    public static GroupState OnLoadDetail(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null, SelectedGroup = null };

    [ReducerMethod]
    public static GroupState OnLoadDetailSuccess(GroupState state, LoadGroupDetailSuccessAction action) =>
        state with { IsLoading = false, SelectedGroup = action.Group };

    [ReducerMethod]
    public static GroupState OnLoadDetailFailure(GroupState state, LoadGroupDetailFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CreateGroupAction))]
    public static GroupState OnCreate(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnCreateSuccess(GroupState state, CreateGroupSuccessAction action) =>
        state with { IsLoading = false, SelectedGroup = action.Group };

    [ReducerMethod]
    public static GroupState OnCreateFailure(GroupState state, CreateGroupFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateGroupAction))]
    public static GroupState OnUpdate(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnUpdateSuccess(GroupState state, UpdateGroupSuccessAction action) =>
        state with { IsLoading = false, SelectedGroup = action.Group };

    [ReducerMethod]
    public static GroupState OnUpdateFailure(GroupState state, UpdateGroupFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Join ──────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(JoinGroupAction))]
    public static GroupState OnJoin(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnJoinSuccess(GroupState state, JoinGroupSuccessAction action) =>
        state with { IsLoading = false, SelectedGroup = action.Group };

    [ReducerMethod]
    public static GroupState OnJoinFailure(GroupState state, JoinGroupFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Regenerate Invite Code ────────────────────────────────────────────────

    [ReducerMethod(typeof(RegenerateInviteCodeAction))]
    public static GroupState OnRegenerate(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnRegenerateSuccess(GroupState state, RegenerateInviteCodeSuccessAction action)
    {
        if (state.SelectedGroup is null || state.SelectedGroup.Id != action.GroupId)
            return state with { IsLoading = false };

        return state with
        {
            IsLoading = false,
            SelectedGroup = state.SelectedGroup with { InviteCode = action.NewInviteCode }
        };
    }

    [ReducerMethod]
    public static GroupState OnRegenerateFailure(GroupState state, RegenerateInviteCodeFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Leave ─────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LeaveGroupAction))]
    public static GroupState OnLeave(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnLeaveSuccess(GroupState state, LeaveGroupSuccessAction action) =>
        state with
        {
            IsLoading = false,
            SelectedGroup = null,
            Groups = state.Groups.Where(g => g.Id != action.GroupId).ToList()
        };

    [ReducerMethod]
    public static GroupState OnLeaveFailure(GroupState state, LeaveGroupFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Delete (global-admin only) ────────────────────────────────────────────

    [ReducerMethod(typeof(DeleteGroupAction))]
    public static GroupState OnDelete(GroupState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static GroupState OnDeleteSuccess(GroupState state, DeleteGroupSuccessAction action) =>
        state with
        {
            IsLoading     = false,
            SelectedGroup = null,
            Groups        = state.Groups.Where(g => g.Id != action.GroupId).ToList()
        };

    [ReducerMethod]
    public static GroupState OnDeleteFailure(GroupState state, DeleteGroupFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear error ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearGroupErrorAction))]
    public static GroupState OnClearError(GroupState state) =>
        state with { ErrorMessage = null };
}
