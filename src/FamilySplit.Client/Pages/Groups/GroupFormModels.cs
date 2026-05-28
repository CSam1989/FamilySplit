namespace FamilySplit.Client.Pages.Groups;

public record CreateGroupFormModel(string Name, string? Description);

public record EditGroupFormModel(string Name, string? Description);

/// <summary>Mutable model bound to the add/edit group member dialog.</summary>
public class GroupMemberFormModel
{
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool UseOverride { get; set; }
    public decimal? WeightOverride { get; set; } = 1.00m;
}
