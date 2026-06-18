namespace FamilySplit.Features.Activities.Create;

/// <summary>Request body for creating a top-level activity or a sub-activity (same shape).</summary>
public record CreateActivityCommand(string Name, string? Description);
