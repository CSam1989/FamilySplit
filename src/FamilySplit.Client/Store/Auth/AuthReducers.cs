using Fluxor;

namespace FamilySplit.Client.Store.Auth;

public static class AuthReducers
{
    [ReducerMethod(typeof(CheckAuthAction))]
    public static AuthState OnCheck(AuthState state) =>
        state with { IsLoading = true, Error = null };

    [ReducerMethod]
    public static AuthState OnSuccess(AuthState state, CheckAuthSuccessAction action) =>
        state with
        {
            IsLoading = false,
            HasChecked = true,
            IsAuthenticated = true,
            IsGlobalAdmin = action.User.IsGlobalAdmin,
            CurrentUser = action.User
        };

    [ReducerMethod(typeof(CheckAuthNotAuthenticatedAction))]
    public static AuthState OnNotAuthenticated(AuthState state) =>
        state with { IsLoading = false, HasChecked = true, IsAuthenticated = false, CurrentUser = null, IsGlobalAdmin = false };

    [ReducerMethod(typeof(SignOutAction))]
    public static AuthState OnSignOut(AuthState state) =>
        new(); // reset to defaults
}
