using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Families.AddMember;

internal static class AddMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/members", async (AddMemberCommand cmd,
                                      AddMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/families/mine/members/{id}", new CreatedResponse(id));
        });
}
