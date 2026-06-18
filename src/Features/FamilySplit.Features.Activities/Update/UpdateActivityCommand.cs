namespace FamilySplit.Features.Activities.Update;

/// <summary>Request body for renaming / re-describing an activity.</summary>
public record UpdateActivityCommand(string Name, string? Description);
