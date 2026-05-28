using Fluxor;
using FamilySplit.Client.Services;

namespace FamilySplit.Client.Store.Auth;

public class AuthEffects
{
    private readonly AuthService _auth;
    private readonly IWhoAmIApi  _whoAmI;

    public AuthEffects(AuthService auth, IWhoAmIApi whoAmI)
    {
        _auth   = auth;
        _whoAmI = whoAmI;
    }

    [EffectMethod(typeof(CheckAuthAction))]
    public async Task HandleCheckAuth(IDispatcher dispatcher)
    {
        // Silent refresh — succeeds if the browser still holds a valid HttpOnly
        // refresh cookie, which is the default for any prior signed-in session.
        if (!await _auth.IsAuthenticatedAsync())
        {
            dispatcher.Dispatch(new CheckAuthNotAuthenticatedAction());
            return;
        }

        try
        {
            var user = await _whoAmI.GetAsync();
            dispatcher.Dispatch(new CheckAuthSuccessAction(user));
        }
        catch
        {
            // JWT was refused even after refresh — fully sign out.
            await _auth.LogoutAsync();
            dispatcher.Dispatch(new CheckAuthNotAuthenticatedAction());
        }
    }

    [EffectMethod(typeof(SignOutAction))]
    public async Task HandleSignOut(IDispatcher dispatcher)
    {
        await _auth.LogoutAsync();
        // State is reset by the reducer; no further dispatch needed.
    }
}
