namespace FamilySplit.Features.Notifications.Unsubscribe;

/// <summary>Body of <c>DELETE /push/unsubscribe</c>. No validator — matches the legacy behaviour, which
/// applied no field validation on this path (unlike Subscribe).</summary>
public sealed record UnsubscribeCommand(string Endpoint);
