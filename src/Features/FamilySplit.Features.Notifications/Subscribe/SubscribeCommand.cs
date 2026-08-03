namespace FamilySplit.Features.Notifications.Subscribe;

/// <summary>Body of <c>POST /push/subscribe</c> — the browser's PushSubscription fields.</summary>
public sealed record SubscribeCommand(string Endpoint, string P256dh, string Auth);
