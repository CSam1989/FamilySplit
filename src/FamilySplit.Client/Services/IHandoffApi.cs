using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// One-shot endpoint that exchanges the HttpOnly handoff cookie for the JWT.
/// Must be called with credentials=include so the browser sends the cookie.
/// The HttpClient registered for this client sets BrowserRequestCredentials.Include
/// via <see cref="System.Net.Http.HttpRequestMessage"/> extensions in Program.cs.
/// </summary>
public interface IHandoffApi
{
    [Get("/auth/handoff")]
    Task<HandoffResponse> GetAsync();
}

public record HandoffResponse(string Token);
