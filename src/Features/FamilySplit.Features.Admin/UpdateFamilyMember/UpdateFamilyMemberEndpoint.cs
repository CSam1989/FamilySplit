using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.UpdateFamilyMember;

internal static class UpdateFamilyMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/families/{familyId:guid}/members/{memberId:guid}", async (Guid familyId, Guid memberId,
                                                                              UpdateFamilyMemberCommand cmd,
                                                                              UpdateFamilyMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(memberId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
