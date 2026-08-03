using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.RemoveMember;

internal static class RemoveMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapDelete("/members/{memberId:guid}", async (Guid memberId,
                                                         RemoveMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(memberId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
