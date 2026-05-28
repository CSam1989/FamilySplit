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
        //
        // Retry once: in the unlikely event a sibling refresh call (e.g. from
        // JwtAuthHandler) is still in-flight when this runs, the first attempt
        // may return false (transient 401) even though a valid token is about to
        // land in memory. The _refreshLock semaphore serialises the HTTP calls, so
        // the second attempt will simply see HasValidToken = true and return
        // immediately without making another network request.
        if (!await _auth.IsAuthenticatedAsync() && !await _auth.IsAuthenticatedAsync())
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
