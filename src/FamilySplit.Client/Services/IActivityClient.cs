using Refit;

namespace FamilySplit.Client.Services;

public interface IActivityClient
{
    [Get("/groups/{groupId}/activities")]
    Task<List<ActivitySummaryDto>> ListAsync(Guid groupId);

    [Get("/groups/{groupId}/activities/{activityId}")]
    Task<ActivityDetailDto> GetAsync(Guid groupId, Guid activityId);

    // Strict CQRS: create returns 201 + { "id": "<guid>" }; the caller re-queries.
    [Post("/groups/{groupId}/activities")]
    Task<CreatedResponse> CreateAsync(Guid groupId, [Body] CreateActivityRequest request);

    // Strict CQRS: update returns 204 No Content; the caller re-queries.
    [Put("/groups/{groupId}/activities/{activityId}")]
    Task UpdateAsync(Guid groupId, Guid activityId, [Body] UpdateActivityRequest request);

    // Strict CQRS: close returns 204 No Content; the caller re-queries.
    [Post("/groups/{groupId}/activities/{activityId}/close")]
    Task CloseAsync(Guid groupId, Guid activityId);

    // Strict CQRS: sub-activity create returns 201 + { "id": "<guid>" }; the caller re-queries.
    [Post("/groups/{groupId}/activities/{activityId}/sub-activities")]
    Task<CreatedResponse> CreateSubActivityAsync(Guid groupId, Guid activityId, [Body] CreateActivityRequest request);

    // Strict CQRS: add participant returns 204 No Content; the caller re-queries.
    [Post("/groups/{groupId}/activities/{activityId}/participants")]
    Task AddParticipantAsync(Guid groupId, Guid activityId, [Body] AddParticipantRequest request);

    // Strict CQRS: remove participant returns 204 No Content; the caller re-queries.
    [Delete("/groups/{groupId}/activities/{activityId}/participants/{memberId}")]
    Task RemoveParticipantAsync(Guid groupId, Guid activityId, Guid memberId);
}
