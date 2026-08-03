using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Groups.RegenerateInviteCode;

internal static class RegenerateInviteCodeEndpoint
{
    // 204 (strict CQRS): GroupDetailDto already exposes the invite code to admins, so the client
    // re-queries the detail rather than consuming a response body.
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/{groupId:guid}/invite-code", async (Guid groupId,
                                                          RegenerateInviteCodeCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            await handler.HandleAsync(groupId, ctx.User.GetUserId(), ct);
            return Results.NoContent();
        });
}
