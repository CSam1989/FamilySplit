using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Activities.Dtos;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateActivityRequest(string Name, string? Description);

public record UpdateActivityRequest(string Name, string? Description);

public record AddParticipantRequest(Guid FamilyMemberId);

// ── Responses ─────────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight summary shown in the activity list for a group.
/// </summary>
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
    DateTimeOffset? ClosedAt,
    int ExpenseCount = 0,
    decimal TotalExpenseAmount = 0m,
    string ExpenseCurrency = "EUR");

/// <summary>
/// Full activity detail including participants and sub-activities.
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

/// <summary>
/// A participant in an activity, with current weight info for display.
/// </summary>
public record ActivityParticipantDto(
    Guid ParticipantId,
    Guid FamilyMemberId,
    string DisplayName,
    Guid FamilyId,
    string FamilyName,
    decimal CurrentWeight,
    WeightTier CurrentTier);
