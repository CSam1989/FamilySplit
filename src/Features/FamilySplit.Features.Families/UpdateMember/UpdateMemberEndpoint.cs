using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.UpdateMember;

internal static class UpdateMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPut("/members/{memberId:guid}", async (Guid memberId, UpdateMemberCommand cmd,
                                                      UpdateMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(memberId, cmd, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
