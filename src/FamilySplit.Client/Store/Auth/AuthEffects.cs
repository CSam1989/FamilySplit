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
            // Token present but rejected (expired etc.) — treat as signed out.
            await _auth.ClearAsync();
            dispatcher.Dispatch(new CheckAuthNotAuthenticatedAction());
        }
    }

    [EffectMethod(typeof(SignOutAction))]
    public async Task HandleSignOut(IDispatcher dispatcher)
    {
        await _auth.ClearAsync();
        // State is reset by the reducer; no further dispatch needed.
    }
}
