using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Profile endpoint — GET /users/me/profile returns the FamilyMember
/// linked to the currently authenticated user.
/// </summary>
public interface IFamilyMemberClient
{
    [Get("/users/me/profile")]
    Task<FamilyMemberDto> GetProfileAsync();
}
