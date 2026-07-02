using FamilySplit.Common.Calculations;
using FamilySplit.Domain.Entities;

namespace FamilySplit.Features.Families.Shared;

/// <summary>
/// Maps a <see cref="FamilyMember"/> entity to <see cref="FamilyMemberDto"/>, snapshotting the
/// current weight + tier via the shared <see cref="WeightCalculator"/>. Pure — used by the query
/// handlers (data access) only.
/// </summary>
internal static class FamilyMemberMapper
{
    public static FamilyMemberDto ToDto(FamilyMember m, DateOnly asOfDate) => new(
        Id: m.Id,
        DisplayName: m.DisplayName,
        Email: m.Email,
        DateOfBirth: m.DateOfBirth,
        WeightOverride: m.WeightOverride,
        CurrentWeight: WeightCalculator.GetWeight(m, asOfDate),
        CurrentTier: WeightCalculator.GetTier(m, asOfDate),
        IsActive: m.IsActive,
        IsLinked: m.UserId is not null,
        IsAdmin: m.IsAdmin,
        CreatedAt: m.CreatedAt);
}
