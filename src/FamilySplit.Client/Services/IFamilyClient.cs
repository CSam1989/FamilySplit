using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Manages the caller's own Family. All routes require a valid JWT.
/// Family admins can add / update / remove members; any member can read.
/// Mutations follow the strict-CQRS response shapes (create → 201 + id, everything
/// else → 204) — the caller re-queries <see cref="GetMyFamilyAsync"/> afterwards.
/// </summary>
public interface IFamilyClient
{
    [Get("/families/mine")]
    Task<FamilyDto> GetMyFamilyAsync();

    [Put("/families/mine")]
    Task UpdateFamilyNameAsync([Body] UpdateFamilyNameRequest request);

    [Post("/families/mine/members")]
    Task<CreatedResponse> AddMemberAsync([Body] AddFamilyMemberRequest request);

    [Put("/families/mine/members/{memberId}")]
    Task UpdateMemberAsync(Guid memberId, [Body] UpdateFamilyMemberRequest request);

    [Delete("/families/mine/members/{memberId}")]
    Task RemoveMemberAsync(Guid memberId);
}
