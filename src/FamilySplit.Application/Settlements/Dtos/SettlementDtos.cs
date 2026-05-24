using FamilySplit.Domain.Enums;

namespace FamilySplit.Application.Settlements.Dtos;

// ── Response DTOs ─────────────────────────────────────────────────────────────

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
