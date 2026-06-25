using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.RemoveFamilyMember;

internal static class RemoveFamilyMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/families/{familyId:guid}/members/{memberId:guid}", async (Guid familyId, Guid memberId,
                                                                                 RemoveFamilyMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(memberId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
