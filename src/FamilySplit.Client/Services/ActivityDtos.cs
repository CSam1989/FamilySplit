using FamilySplit.Domain.Enums;

namespace FamilySplit.Client.Services;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateActivityRequest(string Name, string? Description);

public record UpdateActivityRequest(string Name, string? Description);

public record AddParticipantRequest(Guid FamilyMemberId);

// ── Responses ─────────────────────────────────────────────────────────────────

public record ActivitySummaryDto(
    Guid Id,
    Guid GroupId,
    string Name,
    string? Description,
    ActivityStatus Status,
    Guid? ParentActivityId,
    int ParticipantCount,
    int SubActivityCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ClosedAt);

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

public record ActivityParticipantDto(
    Guid ParticipantId,
    Guid FamilyMemberId,
    string DisplayName,
    Guid FamilyId,
    string FamilyName,
    decimal CurrentWeight,
    WeightTier CurrentTier);
