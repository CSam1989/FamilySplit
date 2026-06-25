using FamilySplit.Common.Exceptions;
using FamilySplit.Common.Security;
using FamilySplit.Features.Settlements.Shared;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Settlements.GetDetail;

/// <summary>
/// Query — full detail for a settlement the caller's family belongs to, including its approval-step
/// history (absorbs the old service's <c>BuildDetailDtoAsync</c>). Pure data access: lookup + group
/// membership guard, then explicit-join projections. Testcontainers-tested.
/// </summary>
public sealed class GetSettlementDetailQueryHandler
{
    private readonly AppDbContext _db;
    private readonly GroupMembershipGuard _guard;
    private readonly ILogger<GetSettlementDetailQueryHandler> _logger;

    public GetSettlementDetailQueryHandler(AppDbContext db, GroupMembershipGuard guard, ILogger<GetSettlementDetailQueryHandler> logger)
    {
        _db = db;
        _guard = guard;
        _logger = logger;
    }

    public async Task<SettlementDetailDto> HandleAsync(Guid settlementId, Guid callerId, CancellationToken ct)
    {
        _logger.LogDebug("Getting settlement detail {SettlementId} for user {UserId}", settlementId, callerId);

        var settlement = await _db.Settlements
            .AsNoTracking()
            .Where(s => s.Id == settlementId)
            .Select(s => new { s.Id, s.ActivityId })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Settlement not found.");

        var activity = await _db.Activities
            .AsNoTracking()
            .Where(a => a.Id == settlement.ActivityId)
            .Select(a => new { a.GroupId })
            .FirstOrDefaultAsync(ct)
            ?? throw ValidationErrors.NotFound("Activity not found.");

        await _guard.RequireGroupMemberAsync(activity.GroupId, callerId, ct);

        var s = await (
            from settlementRow in _db.Settlements.AsNoTracking()
            join pf in _db.Families on settlementRow.PayerFamilyId equals pf.Id
            join rf in _db.Families on settlementRow.ReceiverFamilyId equals rf.Id
            where settlementRow.Id == settlementId
            select new
            {
                settlementRow.Id,
                settlementRow.ActivityId,
                settlementRow.PayerFamilyId,
                PayerFamilyName = pf.Name,
                settlementRow.ReceiverFamilyId,
                ReceiverFamilyName = rf.Name,
                settlementRow.Amount,
                settlementRow.Currency,
                settlementRow.Status,
                settlementRow.Notes,
                settlementRow.ProposedAt,
                settlementRow.CompletedAt,
            }
        ).FirstOrDefaultAsync(ct)
          ?? throw ValidationErrors.NotFound("Settlement not found.");

        var steps = await (
            from step in _db.ApprovalSteps.AsNoTracking()
            join u in _db.Users on step.ApproverId equals u.Id
            where step.SettlementId == settlementId
            orderby step.CreatedAt
            select new ApprovalStepDto(
                step.Id,
                step.ApproverId,
                u.DisplayName,
                step.StepType,
                step.Status,
                step.ActionedAt,
                step.CreatedAt)
        ).ToListAsync(ct);

        return new SettlementDetailDto(
            s.Id,
            s.ActivityId,
            s.PayerFamilyId,
            s.PayerFamilyName,
            s.ReceiverFamilyId,
            s.ReceiverFamilyName,
            s.Amount,
            s.Currency,
            s.Status,
            s.Notes,
            steps,
            s.ProposedAt,
            s.CompletedAt);
    }
}
