using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Global-admin API — all routes require IsGlobalAdmin = true (enforced server-side).
/// </summary>
public interface IAdminClient
{
    [Get("/admin/families")]
    Task<List<FamilyDto>> ListFamiliesAsync();

    [Post("/admin/families")]
    Task<FamilyDto> CreateFamilyAsync([Body] CreateFamilyRequest request);

    [Get("/admin/families/{familyId}")]
    Task<FamilyDto> GetFamilyAsync(Guid familyId);

    [Post("/admin/families/{familyId}/members")]
    Task<FamilyMemberDto> AddMemberAsync(Guid familyId, [Body] AddFamilyMemberRequest request);

    [Put("/admin/families/{familyId}/members/{memberId}")]
    Task<FamilyMemberDto> UpdateMemberAsync(Guid familyId, Guid memberId, [Body] UpdateFamilyMemberRequest request);

    [Delete("/admin/families/{familyId}/members/{memberId}")]
    Task RemoveMemberAsync(Guid familyId, Guid memberId);
}

public record CreateFamilyRequest(string Name);
