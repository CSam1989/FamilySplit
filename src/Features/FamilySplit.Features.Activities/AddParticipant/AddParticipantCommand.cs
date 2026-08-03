namespace FamilySplit.Features.Activities.AddParticipant;

/// <summary>Request body for adding a group member as a participant in an activity.</summary>
public record AddParticipantCommand(Guid FamilyMemberId);
