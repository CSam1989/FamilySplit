using Refit;

namespace FamilySplit.Client.Services;

public interface IWhoAmIApi
{
    [Get("/whoami")]
    Task<WhoAmIResponse> GetAsync();
}

public record WhoAmIResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    string Provider,
    DateTimeOffset CreatedAt,
    bool IsGlobalAdmin);
