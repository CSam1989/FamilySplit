using Fluxor;

namespace FamilySplit.Client.Store.Family;

public static class FamilyReducers
{
    // ── Load ──────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(LoadMyFamilyAction))]
    public static FamilyState OnLoad(FamilyState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static FamilyState OnLoadSuccess(FamilyState state, LoadMyFamilySuccessAction action) =>
        state with { IsLoading = false, MyFamily = action.Family };

    [ReducerMethod]
    public static FamilyState OnLoadFailure(FamilyState state, LoadMyFamilyFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Rename ────────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateFamilyNameAction))]
    public static FamilyState OnRename(FamilyState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static FamilyState OnRenameSuccess(FamilyState state, UpdateFamilyNameSuccessAction action) =>
        state with { IsLoading = false, MyFamily = action.Family };

    [ReducerMethod]
    public static FamilyState OnRenameFailure(FamilyState state, UpdateFamilyNameFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Add Member ────────────────────────────────────────────────────────────

    [ReducerMethod(typeof(AddFamilyMemberAction))]
    public static FamilyState OnAddMember(FamilyState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    // Success triggers a family reload — reducer just clears loading.
    [ReducerMethod(typeof(AddFamilyMemberSuccessAction))]
    public static FamilyState OnAddMemberSuccess(FamilyState state) =>
        state with { IsLoading = false };

    [ReducerMethod]
    public static FamilyState OnAddMemberFailure(FamilyState state, AddFamilyMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Update Member ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(UpdateFamilyMemberAction))]
    public static FamilyState OnUpdateMember(FamilyState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static FamilyState OnUpdateMemberSuccess(FamilyState state, UpdateFamilyMemberSuccessAction action)
    {
        if (state.MyFamily is null) return state with { IsLoading = false };

        var updatedMembers = state.MyFamily.Members
            .Select(m => m.Id == action.Member.Id ? action.Member : m)
            .ToList();

        return state with
        {
            IsLoading = false,
            MyFamily = state.MyFamily with { Members = updatedMembers }
        };
    }

    [ReducerMethod]
    public static FamilyState OnUpdateMemberFailure(FamilyState state, UpdateFamilyMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };

    // ── Remove Member ─────────────────────────────────────────────────────────

    [ReducerMethod(typeof(RemoveFamilyMemberAction))]
    public static FamilyState OnRemoveMember(FamilyState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static FamilyState OnRemoveMemberSuccess(FamilyState state, RemoveFamilyMemberSuccessAction action)
    {
        if (state.MyFamily is null) return state with { IsLoading = false };

        var updatedMembers = state.MyFamily.Members
            .Where(m => m.Id != action.MemberId)
            .ToList();

        return state with
        {
            IsLoading = false,
            MyFamily = state.MyFamily with { Members = updatedMembers }
        };
    }

    [ReducerMethod]
    public static FamilyState OnRemoveMemberFailure(FamilyState state, RemoveFamilyMemberFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };
}
