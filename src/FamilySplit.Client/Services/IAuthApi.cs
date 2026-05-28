using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Refit shape for the refresh-token endpoints. Both calls must run with
/// credentials=include so the HttpOnly refresh cookie is attached — that is
/// wired up via <see cref="IncludeCredentialsHandler"/> in Program.cs.
/// </summary>
public interface IAuthApi
{
    /// <summary>
    /// Rotates the refresh cookie and returns a fresh JWT. Returns 401 when no
    /// refresh cookie exists or it has expired / been revoked.
    /// </summary>
    [Post("/auth/refresh")]
    Task<RefreshResponse> RefreshAsync();

    /// <summary>Revokes the refresh row server-side and clears the cookie.</summary>
    [Post("/auth/logout")]
    Task LogoutAsync();
}

public record RefreshResponse(string Token, int ExpiresInSeconds);
