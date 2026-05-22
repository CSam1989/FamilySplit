using System.Net.Http.Headers;

namespace FamilySplit.Client.Services;

/// <summary>
/// DelegatingHandler that attaches the FamilySplit JWT (from sessionStorage via
/// <see cref="AuthService"/>) as a Bearer token on every outgoing API call.
/// </summary>
public class JwtAuthHandler : DelegatingHandler
{
    private readonly AuthService _auth;

    public JwtAuthHandler(AuthService auth) => _auth = auth;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _auth.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
