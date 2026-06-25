using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.AddFamilyMember;

internal static class AddFamilyMemberEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/families/{familyId:guid}/members", async (Guid familyId, AddFamilyMemberCommand cmd,
                                                               AddFamilyMemberCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(familyId, cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/admin/families/{familyId}/members/{id}", new CreatedResponse(id));
        });
}
