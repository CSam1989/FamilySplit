using FamilySplit.Client.Services;
using Fluxor;

namespace FamilySplit.Client.Store.FamilyMembers;

[FeatureState]
public record FamilyMemberState
{
    /// <summary>The FamilyMember profile linked to the currently logged-in user.</summary>
    public FamilyMemberDto? MyProfile { get; init; }
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
}
