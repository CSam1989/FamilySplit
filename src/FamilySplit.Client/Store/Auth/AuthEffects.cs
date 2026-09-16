using System.Net;
using FamilySplit.Client.Services;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Refit;

namespace FamilySplit.Client.Store.Auth;

public class AuthEffects
{
    private readonly AuthService _auth;
    private readonly IWhoAmIApi _whoAmI;
    private readonly NavigationManager _nav;
    private readonly ILogger<AuthEffects> _logger;

    public AuthEffects(AuthService auth, IWhoAmIApi whoAmI, NavigationManager nav, ILogger<AuthEffects> logger)
    {
        _auth = auth;
        _whoAmI = whoAmI;
        _nav = nav;
        _logger = logger;
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
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // The JWT was genuinely refused even after refresh — fully sign out.
            _logger.LogWarning(ex, "WhoAmI returned 401 after refresh; signing out.");
            await _auth.LogoutAsync();
            dispatcher.Dispatch(new CheckAuthNotAuthenticatedAction());
        }
        catch (Exception ex)
        {
            // Transient failure (5xx / network). Do NOT call LogoutAsync — that would
            // revoke the refresh token and permanently end the session over a blip.
            // Treat as unauthenticated for now; a later check or reload can recover.
            _logger.LogError(ex, "WhoAmI failed during auth check (non-401); leaving session intact.");
            dispatcher.Dispatch(new CheckAuthNotAuthenticatedAction());
        }
    }

    [EffectMethod(typeof(SignOutAction))]
    public async Task HandleSignOut(IDispatcher dispatcher)
    {
        try
        {
            await _auth.LogoutAsync();
        }
        catch (Exception ex)
        {
            // Best-effort server-side revoke; local state is cleared by the reducer
            // regardless, so a failure here must not block navigating away.
            _logger.LogError(ex, "Logout call failed; clearing local session state anyway.");
        }

        // State is reset by the reducer; navigate to home so the login screen is shown.
        _nav.NavigateTo("/");
    }
}
