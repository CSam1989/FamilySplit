using FamilySplit.Application.Families;
using FamilySplit.Application.Families.Dtos;

namespace FamilySplit.Api.Endpoints;

/// <summary>
/// Endpoints for managing the caller's own Family.
/// Family admins (IsAdmin = true on their FamilyMember) can add / update / remove members.
/// Any member can read their own family and update their own profile.
/// </summary>
public static class FamilyEndpoints
{
    public static WebApplication MapFamilyEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/families/mine")
            .RequireAuthorization()
            .WithTags("Family");

        // GET /families/mine — full family with all members
        grp.MapGet("/", async (FamilyService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var family = await svc.GetMyFamilyAsync(callerId);
            return family is null ? Results.NotFound() : Results.Ok(family);
        });

        // PUT /families/mine — rename the family (admin only)
        grp.MapPut("/", async (UpdateFamilyNameRequest req, FamilyService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var family = await svc.UpdateFamilyNameAsync(req, callerId);
            return Results.Ok(family);
        });

        // POST /families/mine/members — add a member (admin only)
        grp.MapPost("/members", async (AddFamilyMemberRequest req, FamilyService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var member = await svc.AddMemberAsync(req, callerId);
            return Results.Created($"/families/mine/members/{member.Id}", member);
        });

        // PUT /families/mine/members/{memberId} — update a member (admin or self)
        grp.MapPut("/members/{memberId:guid}",
            async (Guid memberId, UpdateFamilyMemberRequest req, FamilyService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                var member = await svc.UpdateMemberAsync(memberId, req, callerId);
                return Results.Ok(member);
            });

        // DELETE /families/mine/members/{memberId} — soft-delete a member (admin only)
        grp.MapDelete("/members/{memberId:guid}",
            async (Guid memberId, FamilyService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                await svc.RemoveMemberAsync(memberId, callerId);
                return Results.NoContent();
            });

        return app;
    }
}
