using FamilySplit.Application.Admin;
using FamilySplit.Application.Admin.Dtos;
using FamilySplit.Application.Families.Dtos;

namespace FamilySplit.Api.Endpoints;

/// <summary>
/// Global-admin endpoints. All routes require the caller to have
/// <c>User.IsGlobalAdmin = true</c> (enforced inside AdminService).
/// </summary>
public static class AdminEndpoints
{
    public static WebApplication MapAdminEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/admin")
            .RequireAuthorization()
            .WithTags("Admin");

        // GET /admin/families — list all families
        grp.MapGet("/families", async (AdminService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var families = await svc.ListFamiliesAsync(callerId);
            return Results.Ok(families);
        });

        // POST /admin/families — create a new family
        grp.MapPost("/families", async (CreateFamilyRequest req, AdminService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var family = await svc.CreateFamilyAsync(req, callerId);
            return Results.Created($"/admin/families/{family.Id}", family);
        });

        // GET /admin/families/{familyId} — get one family with members
        grp.MapGet("/families/{familyId:guid}", async (Guid familyId, AdminService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var family = await svc.GetFamilyAsync(familyId, callerId);
            return Results.Ok(family);
        });

        // POST /admin/families/{familyId}/members — add a member to a family
        grp.MapPost("/families/{familyId:guid}/members",
            async (Guid familyId, AddFamilyMemberRequest req, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                var member = await svc.AddFamilyMemberAsync(familyId, req, callerId);
                return Results.Created($"/admin/families/{familyId}/members/{member.Id}", member);
            });

        // PUT /admin/families/{familyId}/members/{memberId} — update any member
        grp.MapPut("/families/{familyId:guid}/members/{memberId:guid}",
            async (Guid familyId, Guid memberId, UpdateFamilyMemberRequest req, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                var member = await svc.UpdateFamilyMemberAsync(memberId, req, callerId);
                return Results.Ok(member);
            });

        // DELETE /admin/families/{familyId}/members/{memberId} — soft-delete a member
        grp.MapDelete("/families/{familyId:guid}/members/{memberId:guid}",
            async (Guid familyId, Guid memberId, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                await svc.RemoveFamilyMemberAsync(memberId, callerId);
                return Results.NoContent();
            });

        // DELETE /admin/groups/{groupId} — hard-delete a group (cascades to members, activities)
        grp.MapDelete("/groups/{groupId:guid}",
            async (Guid groupId, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                await svc.DeleteGroupAsync(groupId, callerId);
                return Results.NoContent();
            });

        // POST /admin/groups/{groupId}/families — add a family to a group
        grp.MapPost("/groups/{groupId:guid}/families",
            async (Guid groupId, AdminAddFamilyToGroupRequest req, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                await svc.AddFamilyToGroupAsync(groupId, req.FamilyId, callerId);
                return Results.NoContent();
            });

        // DELETE /admin/groups/{groupId}/families/{familyId} — remove a family from a group
        grp.MapDelete("/groups/{groupId:guid}/families/{familyId:guid}",
            async (Guid groupId, Guid familyId, AdminService svc, HttpContext ctx) =>
            {
                var callerId = ctx.User.GetUserId();
                await svc.RemoveFamilyFromGroupAsync(groupId, familyId, callerId);
                return Results.NoContent();
            });

        return app;
    }
}
