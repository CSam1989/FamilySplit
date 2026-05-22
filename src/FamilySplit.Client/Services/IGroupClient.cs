using Refit;

namespace FamilySplit.Client.Services;

public interface IGroupClient
{
    [Get("/groups")]
    Task<List<GroupSummaryDto>> ListAsync();

    [Get("/groups/{groupId}")]
    Task<GroupDetailDto> GetAsync(Guid groupId);

    [Post("/groups")]
    Task<GroupDetailDto> CreateAsync([Body] CreateGroupRequest request);

    [Put("/groups/{groupId}")]
    Task<GroupDetailDto> UpdateAsync(Guid groupId, [Body] UpdateGroupRequest request);

    [Post("/groups/join")]
    Task<GroupDetailDto> JoinAsync([Body] JoinGroupRequest request);

    [Post("/groups/{groupId}/invite-code")]
    Task<RegenerateInviteCodeResponse> RegenerateInviteCodeAsync(Guid groupId);
}

public record RegenerateInviteCodeResponse(string InviteCode);
