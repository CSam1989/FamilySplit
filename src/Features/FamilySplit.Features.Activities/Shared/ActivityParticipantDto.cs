using FamilySplit.Domain.Enums;

namespace FamilySplit.Features.Activities.Shared;

/// <summary>
/// A participant in an activity, with current weight info for display. Produced
/// by the GetDetail query (read-side wire-format lock).
/// </summary>
public record ActivityParticipantDto(
    Guid ParticipantId,
    Guid FamilyMemberId,
    string DisplayName,
    Guid FamilyId,
    string FamilyName,
    decimal CurrentWeight,
    WeightTier CurrentTier);
