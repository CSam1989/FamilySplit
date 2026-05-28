using Refit;

namespace FamilySplit.Client.Services;

public interface IPushClient
{
    [Get("/push/vapid-public-key")]
    Task<VapidPublicKeyResponse> GetVapidPublicKeyAsync();

    [Post("/push/subscribe")]
    Task SubscribeAsync([Body] PushSubscribeRequest request);

    [Delete("/push/unsubscribe")]
    Task UnsubscribeAsync([Body] PushUnsubscribeRequest request);
}

public record VapidPublicKeyResponse(string PublicKey);
public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);
public record PushUnsubscribeRequest(string Endpoint);
