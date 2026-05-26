using FamilySplit.Domain.Enums;

namespace FamilySplit.Client.Services;

public record FamilyBalanceDto(
    Guid FamilyId,
    string FamilyName,
    decimal Balance,
    string Currency);

public record ApprovalStepDto(
    Guid Id,
    Guid ApproverId,
    string ApproverName,
    StepType StepType,
    StepStatus Status,
    DateTimeOffset? ActionedAt,
    DateTimeOffset CreatedAt);

public record SettlementSummaryDto(
    Guid Id,
    Guid ActivityId,
    Guid PayerFamilyId,
    string PayerFamilyName,
    Guid ReceiverFamilyId,
    string ReceiverFamilyName,
    decimal Amount,
    string Currency,
    SettlementStatus Status,
    DateTimeOffset ProposedAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// Settlement summary enriched with group/activity context — used for group-level and dashboard views.
/// GroupId is required to call confirm-sent / confirm-received from any page.
/// </summary>
public record GroupSettlementSummaryDto(
    Guid Id,
    Guid GroupId,
    Guid ActivityId,
    string ActivityName,
    Guid PayerFamilyId,
    string PayerFamilyName,
    Guid ReceiverFamilyId,
    string ReceiverFamilyName,
    decimal Amount,
    string Currency,
    SettlementStatus Status,
    DateTimeOffset ProposedAt);

public record SettlementDetailDto(
    Guid Id,
    Guid ActivityId,
    Guid PayerFamilyId,
    string PayerFamilyName,
    Guid ReceiverFamilyId,
    string ReceiverFamilyName,
    decimal Amount,
    string Currency,
    SettlementStatus Status,
    string? Notes,
    List<ApprovalStepDto> ApprovalSteps,
    DateTimeOffset ProposedAt,
    DateTimeOffset? CompletedAt);
