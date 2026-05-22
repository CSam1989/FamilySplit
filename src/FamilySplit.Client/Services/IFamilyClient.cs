using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Manages the caller's own Family. All routes require a valid JWT.
/// Family admins can add / update / remove members; any member can read.
/// </summary>
public interface IFamilyClient
{
    [Get("/families/mine")]
    Task<FamilyDto> GetMyFamilyAsync();

    [Put("/families/mine")]
    Task<FamilyDto> UpdateFamilyNameAsync([Body] UpdateFamilyNameRequest request);

    [Post("/families/mine/members")]
    Task<FamilyMemberDto> AddMemberAsync([Body] AddFamilyMemberRequest request);

    [Put("/families/mine/members/{memberId}")]
    Task<FamilyMemberDto> UpdateMemberAsync(Guid memberId, [Body] UpdateFamilyMemberRequest request);

    [Delete("/families/mine/members/{memberId}")]
    Task RemoveMemberAsync(Guid memberId);
}
