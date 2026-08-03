using Refit;

namespace FamilySplit.Client.Services;

public interface IGroupClient
{
    [Get("/groups")]
    Task<List<GroupSummaryDto>> ListAsync();

    [Get("/groups/{groupId}")]
    Task<GroupDetailDto> GetAsync(Guid groupId);

    // Strict CQRS: create returns 201 + { "id": "<guid>" }; the caller re-queries.
    [Post("/groups")]
    Task<CreatedResponse> CreateAsync([Body] CreateGroupRequest request);

    // Strict CQRS: update returns 204 No Content; the caller re-queries.
    [Put("/groups/{groupId}")]
    Task UpdateAsync(Guid groupId, [Body] UpdateGroupRequest request);

    // Documented exception: join returns 200 + { "id": "<groupId>" }; the caller re-queries.
    [Post("/groups/join")]
    Task<CreatedResponse> JoinAsync([Body] JoinGroupRequest request);

    // Strict CQRS: regenerate returns 204 No Content; the new code is obtained by
    // re-fetching the group detail (GroupDetailDto.InviteCode carries it for admins).
    [Post("/groups/{groupId}/invite-code")]
    Task RegenerateInviteCodeAsync(Guid groupId);

    [Delete("/groups/{groupId}/leave")]
    Task LeaveAsync(Guid groupId);
}
