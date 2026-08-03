using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Shared;

namespace FamilySplit.Features.Activities.GetDetail;

/// <summary>
/// Full activity detail including participants and sub-activities. Produced by the GetDetail query
/// (read-side wire-format lock).
/// </summary>
public record ActivityDetailDto(
    Guid Id,
    Guid GroupId,
    string Name,
    string? Description,
    ActivityStatus Status,
    Guid? ParentActivityId,
    IReadOnlyList<ActivityParticipantDto> Participants,
    IReadOnlyList<ActivitySummaryDto> SubActivities,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);
