using Refit;

namespace FamilySplit.Client.Services;

[Headers("Content-Type: application/json")]
public interface ISettlementClient
{
    [Get("/groups/{groupId}/activities/{activityId}/settlements")]
    Task<List<SettlementSummaryDto>> ListAsync(Guid groupId, Guid activityId);

    // Strict CQRS (Phase 8): generate / confirm-sent / confirm-received return 204; the client re-queries.
    [Post("/groups/{groupId}/activities/{activityId}/settlements")]
    Task GenerateAsync(Guid groupId, Guid activityId);

    [Get("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}")]
    Task<SettlementDetailDto> GetDetailAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Post("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-sent")]
    Task ConfirmSentAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Post("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-received")]
    Task ConfirmReceivedAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Get("/groups/{groupId}/activities/{activityId}/balances")]
    Task<List<FamilyBalanceDto>> GetBalancesAsync(Guid groupId, Guid activityId);

    [Get("/groups/{groupId}/settlements")]
    Task<List<GroupSettlementSummaryDto>> ListForGroupAsync(Guid groupId);

    [Get("/settlements/pending")]
    Task<List<GroupSettlementSummaryDto>> ListMyPendingAsync();
}
