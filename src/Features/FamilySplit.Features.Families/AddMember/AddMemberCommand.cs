namespace FamilySplit.Features.Families.AddMember;

/// <summary>Sent when a family admin adds a new member to their own family.</summary>
public record AddMemberCommand(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);
