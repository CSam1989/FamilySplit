using FamilySplit.Application.Groups;
using FamilySplit.Application.Groups.Dtos;

namespace FamilySplit.Api.Endpoints;

public static class GroupsEndpoints
{
    public static WebApplication MapGroupEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/groups")
            .WithTags("Groups");

        // GET /groups — list caller's groups
        grp.MapGet("/", async (GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var groups = await svc.ListAsync(callerId, ct);
            return Results.Ok(groups);
        });

        // GET /groups/{groupId} — group detail
        grp.MapGet("/{groupId:guid}", async (Guid groupId, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.GetDetailAsync(groupId, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups — create group
        grp.MapPost("/", async (CreateGroupRequest req, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.CreateAsync(req, callerId, ct);
            return Results.Created($"/groups/{detail.Id}", detail);
        });

        // PUT /groups/{groupId} — update group (admin only)
        grp.MapPut("/{groupId:guid}", async (Guid groupId, UpdateGroupRequest req, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.UpdateAsync(groupId, req, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/join — join via invite code
        grp.MapPost("/join", async (JoinGroupRequest req, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.JoinAsync(req, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/{groupId}/invite-code — regenerate invite code (admin only)
        grp.MapPost("/{groupId:guid}/invite-code", async (Guid groupId, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var newCode = await svc.RegenerateInviteCodeAsync(groupId, callerId, ct);
            return Results.Ok(new { inviteCode = newCode });
        });

        // DELETE /groups/{groupId}/leave — leave a group (family admin only)
        grp.MapDelete("/{groupId:guid}/leave", async (Guid groupId, GroupService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            await svc.LeaveAsync(groupId, callerId, ct);
            return Results.NoContent();
        });

        return app;
    }
}
