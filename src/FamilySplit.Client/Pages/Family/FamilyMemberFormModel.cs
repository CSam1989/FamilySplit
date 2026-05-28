namespace FamilySplit.Client.Pages.Family;

/// <summary>Mutable model bound to the add/edit family member dialog form fields.</summary>
public class FamilyMemberFormModel
{
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool UseOverride { get; set; }
    public decimal? WeightOverride { get; set; } = 1.00m;
    public bool IsAdmin { get; set; }
}
