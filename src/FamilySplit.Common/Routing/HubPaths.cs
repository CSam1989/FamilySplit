namespace FamilySplit.Common.Routing;

/// <summary>
/// SignalR hub paths. Pinned here because two slices share them: the Auth slice's
/// JwtBearer OnMessageReceived reads ?access_token= only for requests under
/// <see cref="Prefix"/>, and the Notifications slice maps its hub at
/// <see cref="Notifications"/>.
/// </summary>
public static class HubPaths
{
    public const string Prefix = "/hubs";
    public const string Notifications = "/hubs/notifications";
}
