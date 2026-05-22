using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FamilySplit.Client.Services;

/// <summary>
/// DelegatingHandler that sets <c>credentials=include</c> on every outgoing fetch.
/// Required so that the browser sends the HttpOnly handoff cookie cross-origin from
/// the Blazor app at :5001 back to the API at :5081 during the OAuth handoff.
/// </summary>
public class IncludeCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return base.SendAsync(request, cancellationToken);
    }
}
