using Refit;

namespace FamilySplit.Client.Services;

public interface IActivityClient
{
    [Get("/groups/{groupId}/activities")]
    Task<List<ActivitySummaryDto>> ListAsync(Guid groupId);

    [Get("/groups/{groupId}/activities/{activityId}")]
    Task<ActivityDetailDto> GetAsync(Guid groupId, Guid activityId);

    [Post("/groups/{groupId}/activities")]
    Task<ActivityDetailDto> CreateAsync(Guid groupId, [Body] CreateActivityRequest request);

    [Put("/groups/{groupId}/activities/{activityId}")]
    Task<ActivityDetailDto> UpdateAsync(Guid groupId, Guid activityId, [Body] UpdateActivityRequest request);

    [Post("/groups/{groupId}/activities/{activityId}/close")]
    Task<ActivityDetailDto> CloseAsync(Guid groupId, Guid activityId);

    [Post("/groups/{groupId}/activities/{activityId}/sub-activities")]
    Task<ActivityDetailDto> CreateSubActivityAsync(Guid groupId, Guid activityId, [Body] CreateActivityRequest request);

    [Post("/groups/{groupId}/activities/{activityId}/participants")]
    Task<ActivityDetailDto> AddParticipantAsync(Guid groupId, Guid activityId, [Body] AddParticipantRequest request);

    [Delete("/groups/{groupId}/activities/{activityId}/participants/{memberId}")]
    Task<ActivityDetailDto> RemoveParticipantAsync(Guid groupId, Guid activityId, Guid memberId);
}
