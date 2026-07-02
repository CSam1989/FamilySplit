namespace FamilySplit.Features.Families.UpdateMember;

public record UpdateMemberCommand(
    string DisplayName,
    string? Email,
    DateOnly? DateOfBirth,
    decimal? WeightOverride,
    bool IsAdmin = false);
