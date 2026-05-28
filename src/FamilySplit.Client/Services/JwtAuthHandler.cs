using System.Net;
using System.Net.Http.Headers;

namespace FamilySplit.Client.Services;

/// <summary>
/// Attaches the in-memory JWT as a Bearer token. On a 401 response, attempts a
/// single silent refresh via <see cref="AuthService.TryRefreshAsync"/> and
/// retries the request once with the new token. Anything still 401 after the
/// retry is bubbled up so the UI can route to sign-in.
/// </summary>
public class JwtAuthHandler : DelegatingHandler
{
    private readonly AuthService _auth;

    public JwtAuthHandler(AuthService auth) => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        // Try one silent refresh + retry. Dispose the original response first
        // so we don't leak its socket.
        response.Dispose();

        var refreshed = await _auth.TryRefreshAsync();
        if (!refreshed)
        {
            _auth.ClearTokenInMemory();
            // Surface a fresh 401 so callers can route the user to /.
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
            };
        }

        await AttachTokenAsync(request);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        var token = await _auth.GetTokenAsync();
        request.Headers.Authorization = string.IsNullOrEmpty(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }
}
