using Refit;

namespace FamilySplit.Client.Services;

[Headers("Content-Type: application/json")]
public interface ISettlementClient
{
    [Get("/groups/{groupId}/activities/{activityId}/settlements")]
    Task<List<SettlementSummaryDto>> ListAsync(Guid groupId, Guid activityId);

    [Post("/groups/{groupId}/activities/{activityId}/settlements")]
    Task<List<SettlementSummaryDto>> GenerateAsync(Guid groupId, Guid activityId);

    [Get("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}")]
    Task<SettlementDetailDto> GetDetailAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Post("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-sent")]
    Task<SettlementDetailDto> ConfirmSentAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Post("/groups/{groupId}/activities/{activityId}/settlements/{settlementId}/confirm-received")]
    Task<SettlementDetailDto> ConfirmReceivedAsync(Guid groupId, Guid activityId, Guid settlementId);

    [Get("/groups/{groupId}/activities/{activityId}/balances")]
    Task<List<FamilyBalanceDto>> GetBalancesAsync(Guid groupId, Guid activityId);
}
