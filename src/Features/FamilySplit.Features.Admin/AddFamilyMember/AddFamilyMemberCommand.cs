namespace FamilySplit.Features.Admin.AddFamilyMember;

/// <summary>Sent when a global admin adds a new member to a family.</summary>
public record AddFamilyMemberCommand(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);
