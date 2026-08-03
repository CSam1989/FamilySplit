using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Notifications.GetVapidPublicKey;

/// <summary>
/// Query — reads the VAPID public key from configuration. No <c>AppDbContext</c> dependency (there
/// is nothing to read from the database), but classified as a Query for consistency: it is pure data
/// access (config), holds no business rules, and is anonymous — the client needs the key before it
/// can subscribe.
/// </summary>
public sealed class GetVapidPublicKeyQueryHandler
{
    private readonly IConfiguration _config;
    private readonly ILogger<GetVapidPublicKeyQueryHandler> _logger;

    public GetVapidPublicKeyQueryHandler(IConfiguration config, ILogger<GetVapidPublicKeyQueryHandler> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Handle()
    {
        _logger.LogDebug("Fetching VAPID public key");

        var key = _config["Push:Vapid:PublicKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Push:Vapid:PublicKey is not configured. " +
                "Generate VAPID keys and store them in user secrets / env vars.");

        return key;
    }
}
