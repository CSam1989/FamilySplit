using Refit;

namespace FamilySplit.Client.Services;

public interface IHealthApi
{
    [Get("/health")]
    Task<HealthResponse> GetAsync();
}

public record HealthResponse(string Status, string Service, DateTimeOffset Utc);
