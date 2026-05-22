using Fluxor;

namespace FamilySplit.Client.Store.FamilyMembers;

public static class FamilyMemberReducers
{
    [ReducerMethod(typeof(LoadMyProfileAction))]
    public static FamilyMemberState OnLoad(FamilyMemberState state) =>
        state with { IsLoading = true, ErrorMessage = null };

    [ReducerMethod]
    public static FamilyMemberState OnLoadSuccess(FamilyMemberState state, LoadMyProfileSuccessAction action) =>
        state with { IsLoading = false, MyProfile = action.Profile };

    [ReducerMethod]
    public static FamilyMemberState OnLoadFailure(FamilyMemberState state, LoadMyProfileFailureAction action) =>
        state with { IsLoading = false, ErrorMessage = action.ErrorMessage };
}
