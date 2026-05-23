using Fluxor;

namespace FamilySplit.Client.Store.Admin;

public static class AdminReducers
{
    // ── Load families ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadAdminFamiliesAction))]
    public static AdminState OnLoad(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static AdminState OnLoadSuccess(AdminState state, LoadAdminFamiliesSuccessAction action) =>
        state with { IsLoading = false, Families = action.Families };

    [ReducerMethod]
    public static AdminState OnLoadFailure(AdminState state, LoadAdminFamiliesFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Load one family ───────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadAdminFamilyAction))]
    public static AdminState OnLoadOne(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null, SelectedFamily = null };

    [ReducerMethod]
    public static AdminState OnLoadOneSuccess(AdminState state, LoadAdminFamilySuccessAction action) =>
        state with { IsLoading = false, SelectedFamily = action.Family };

    [ReducerMethod]
    public static AdminState OnLoadOneFailure(AdminState state, LoadAdminFamilyFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Create family ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(CreateAdminFamilyAction))]
    public static AdminState OnCreate(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static AdminState OnCreateSuccess(AdminState state, CreateAdminFamilySuccessAction action) =>
        state with
        {
            IsLoading      = false,
            SelectedFamily = action.Family,
            Families       = [.. state.Families, action.Family]
        };

    [ReducerMethod]
    public static AdminState OnCreateFailure(AdminState state, CreateAdminFamilyFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Add member — triggers reload, reducer just clears loading ─────────────

    [ReducerMethod(typeof(AddAdminMemberAction))]
    public static AdminState OnAddMember(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod(typeof(AddAdminMemberSuccessAction))]
    public static AdminState OnAddMemberSuccess(AdminState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static AdminState OnAddMemberFailure(AdminState state, AddAdminMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update member ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateAdminMemberAction))]
    public static AdminState OnUpdateMember(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static AdminState OnUpdateMemberSuccess(AdminState state, UpdateAdminMemberSuccessAction action)
    {
        if (state.SelectedFamily is null) return state with { IsLoading = false };

        var updated = state.SelectedFamily.Members
            .Select(m => m.Id == action.Member.Id ? action.Member : m)
            .ToList();

        return state with
        {
            IsLoading      = false,
            SelectedFamily = state.SelectedFamily with { Members = updated }
        };
    }

    [ReducerMethod]
    public static AdminState OnUpdateMemberFailure(AdminState state, UpdateAdminMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Remove member ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(RemoveAdminMemberAction))]
    public static AdminState OnRemoveMember(AdminState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static AdminState OnRemoveMemberSuccess(AdminState state, RemoveAdminMemberSuccessAction action)
    {
        if (state.SelectedFamily is null) return state with { IsLoading = false };

        var updated = state.SelectedFamily.Members
            .Where(m => m.Id != action.MemberId)
            .ToList();

        return state with
        {
            IsLoading      = false,
            SelectedFamily = state.SelectedFamily with { Members = updated }
        };
    }

    [ReducerMethod]
    public static AdminState OnRemoveMemberFailure(AdminState state, RemoveAdminMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Clear error ───────────────────────────────────────────────────────────

    [ReducerMethod(typeof(ClearAdminErrorAction))]
    public static AdminState OnClearError(AdminState state) =>
        state with { ErrorMessage = null };
}
