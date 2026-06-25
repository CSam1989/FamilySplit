namespace FamilySplit.Features.Admin.UpdateFamilyMember;

public record UpdateFamilyMemberCommand(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);
