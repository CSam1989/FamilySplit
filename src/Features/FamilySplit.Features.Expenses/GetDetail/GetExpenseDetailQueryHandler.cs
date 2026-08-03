using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Features.Expenses.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Expenses.GetDetail;

/// <summary>
/// Query — returns the full detail of one expense, including every participant's
/// snapshotted weight and calculated share. Absorbs the old service's
/// <c>BuildDetailDtoAsync</c> helper. Pure data access: authorization guard then
/// <c>AsNoTracking</c> projections, no mutation.
/// </summary>
public sealed class GetExpenseDetailQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<GetExpenseDetailQueryHandler> _logger;

    public GetExpenseDetailQueryHandler(
        AppDbContext db,
        GroupMembershipGuard guard,
        ILogger<GetExpenseDetailQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<ExpenseDetailDto> HandleAsync(Guid expenseId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Getting expense detail {ExpenseId} for user {UserId}", expenseId, callerId);

        var expense = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Id == expenseId)
            .Select(e => new { e.Id, e.ActivityId, e.Title, e.Description, e.TotalAmount, e.Currency, e.ExpenseDate, e.PaidByUserId, e.Status, e.CreatedAt, e.UpdatedAt })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Expense not found.");

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == expense.ActivityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        // Payer info.
        var payer = await (
            from fm in _db.FamilyMembers.AsNoTracking()
            join f in _db.Families on fm.FamilyId equals f.Id
            where fm.UserId == expense.PaidByUserId && fm.IsActive
            select new { fm.DisplayName, fm.FamilyId, FamilyName = f.Name }
        ).FirstOrDefaultAsync(ct);

        // Participants with family info.
        var participants = await (
            from ep in _db.ExpenseParticipants.AsNoTracking()
            join fm in _db.FamilyMembers on ep.FamilyMemberId equals fm.Id
            join f in _db.Families on fm.FamilyId equals f.Id
            where ep.ExpenseId == expense.Id
            orderby f.Name, fm.DisplayName
            select new
            {
                ep.Id,
                ep.FamilyMemberId,
                fm.DisplayName,
                fm.FamilyId,
                FamilyName = f.Name,
                ep.WeightSnapshot,
                ep.CalculatedAmount,
                ep.IsExcluded,
            }
        ).ToListAsync(ct);

        var participantDtos = participants.Select(p => new ExpenseParticipantDto(
            p.Id,
            p.FamilyMemberId,
            p.DisplayName,
            p.FamilyId,
            p.FamilyName,
            p.WeightSnapshot,
            p.CalculatedAmount,
            p.IsExcluded)).ToList();

        return new ExpenseDetailDto(
            expense.Id,
            expense.ActivityId,
            expense.Title,
            expense.Description,
            expense.TotalAmount,
            expense.Currency,
            expense.ExpenseDate,
            payer?.DisplayName ?? "Unknown",
            payer?.FamilyId ?? Guid.Empty,
            payer?.FamilyName ?? "Unknown",
            expense.Status,
            participantDtos,
            expense.CreatedAt,
            expense.UpdatedAt);
    }
}
