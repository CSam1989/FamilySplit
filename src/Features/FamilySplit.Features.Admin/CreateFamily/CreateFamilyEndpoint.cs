using FamilySplit.Common.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FamilySplit.Features.Admin.CreateFamily;

internal static class CreateFamilyEndpoint
{
    internal static void Map(RouteGroupBuilder grp) =>
        grp.MapPost("/families", async (CreateFamilyCommand cmd,
                                        CreateFamilyCommandHandler handler, HttpContext ctx, CancellationToken ct) =>
        {
            var id = await handler.HandleAsync(cmd, ctx.User.GetUserId(), ct);
            return Results.Created($"/admin/families/{id}", new CreatedResponse(id));
        });
}
