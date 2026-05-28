using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace FamilySplit.Client.Services;

/// <summary>
/// Manages the SignalR connection to /hubs/notifications.
///
/// Usage:
///   • Call ConnectAsync() once after the user is authenticated.
///   • Subscribe to OnNotificationReceived to show snackbars.
///   • Call DisconnectAsync() on logout.
///
/// The JWT is passed as ?access_token= because Blazor WASM WebSocket upgrades
/// cannot set the Authorization header.
/// </summary>
public class NotificationHubConnection : IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly AuthService    _authService;
    private readonly ILogger<NotificationHubConnection> _logger;

    private HubConnection? _hub;

    /// <summary>Fired whenever the server calls ReceiveNotification on this client.</summary>
    public event Action<NotificationMessage>? OnNotificationReceived;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public NotificationHubConnection(
        IConfiguration config,
        AuthService    authService,
        ILogger<NotificationHubConnection> logger)
    {
        _config      = config;
        _authService = authService;
        _logger      = logger;
    }

    // ── Connect ───────────────────────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_hub?.State == HubConnectionState.Connected) return;

        // Dispose any stale connection first.
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }

        var baseUrl = _config["Api:BaseUrl"] ?? "https://localhost:5081";
        var hubUrl  = $"{baseUrl.TrimEnd('/')}/hubs/notifications";

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                // Blazor WASM can't set Authorization headers on WebSocket upgrades —
                // pass the JWT as a query-string parameter instead.
                options.AccessTokenProvider = () =>
                    _authService.GetTokenAsync().AsTask();
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<NotificationMessage>("ReceiveNotification", msg =>
        {
            _logger.LogDebug("SignalR notification received: {Title}", msg.Title);
            OnNotificationReceived?.Invoke(msg);
        });

        _hub.Closed += ex =>
        {
            _logger.LogWarning(ex, "SignalR connection closed");
            return Task.CompletedTask;
        };

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected ({ConnectionId})", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _hub.StartAsync(ct);
            _logger.LogInformation("SignalR connected to {HubUrl}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub at {HubUrl}", hubUrl);
        }
    }

    // ── Disconnect ────────────────────────────────────────────────────────────

    public async Task DisconnectAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
            _logger.LogInformation("SignalR disconnected");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}

/// <summary>Payload sent by the server on ReceiveNotification.</summary>
public record NotificationMessage(string Title, string Message, string Url);
