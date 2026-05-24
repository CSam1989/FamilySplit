using FamilySplit.Domain.Enums;

namespace FamilySplit.Client.Services;

// ── Requests ─────────────────────────────────────────────────────────────────

public record CreateExpenseRequest(
    string Title,
    string? Description,
    decimal TotalAmount,
    string? Currency,
    DateOnly ExpenseDate,
    Guid? CategoryId);

public record UpdateExpenseRequest(
    string Title,
    string? Description,
    decimal TotalAmount,
    string? Currency,
    DateOnly ExpenseDate,
    Guid? CategoryId);

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record ExpenseParticipantDto(
    Guid Id,
    Guid FamilyMemberId,
    string DisplayName,
    Guid FamilyId,
    string FamilyName,
    decimal WeightSnapshot,
    decimal CalculatedAmount,
    bool IsExcluded);

public record ExpenseSummaryDto(
    Guid Id,
    Guid ActivityId,
    string Title,
    string? Description,
    decimal TotalAmount,
    string Currency,
    DateOnly ExpenseDate,
    string PaidByName,
    Guid PaidByFamilyId,
    string PaidByFamilyName,
    ExpenseStatus Status,
    int ParticipantCount,
    DateTimeOffset CreatedAt);

public record ExpenseDetailDto(
    Guid Id,
    Guid ActivityId,
    string Title,
    string? Description,
    decimal TotalAmount,
    string Currency,
    DateOnly ExpenseDate,
    string PaidByName,
    Guid PaidByFamilyId,
    string PaidByFamilyName,
    ExpenseStatus Status,
    List<ExpenseParticipantDto> Participants,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
