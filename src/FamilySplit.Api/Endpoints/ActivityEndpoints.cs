using FamilySplit.Application.Activities;
using FamilySplit.Application.Activities.Dtos;

namespace FamilySplit.Api.Endpoints;

public static class ActivityEndpoints
{
    public static WebApplication MapActivityEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/groups/{groupId:guid}/activities")
            .WithTags("Activities");

        // GET /groups/{groupId}/activities — list top-level activities in a group
        grp.MapGet("/", async (Guid groupId, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var activities = await svc.ListAsync(groupId, callerId, ct);
            return Results.Ok(activities);
        });

        // POST /groups/{groupId}/activities — create top-level activity
        grp.MapPost("/", async (Guid groupId, CreateActivityRequest req, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.CreateAsync(groupId, req, callerId, ct);
            return Results.Created($"/groups/{groupId}/activities/{detail.Id}", detail);
        });

        // GET /groups/{groupId}/activities/{activityId} — get activity detail
        grp.MapGet("/{activityId:guid}", async (Guid groupId, Guid activityId, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.GetDetailAsync(activityId, callerId, ct);
            return Results.Ok(detail);
        });

        // PUT /groups/{groupId}/activities/{activityId} — update activity
        grp.MapPut("/{activityId:guid}", async (Guid groupId, Guid activityId, UpdateActivityRequest req, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.UpdateAsync(activityId, req, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/{groupId}/activities/{activityId}/close — close activity (absorbs open subs)
        grp.MapPost("/{activityId:guid}/close", async (Guid groupId, Guid activityId, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.CloseAsync(activityId, callerId, ct);
            return Results.Ok(detail);
        });

        // POST /groups/{groupId}/activities/{activityId}/sub-activities — create sub-activity
        grp.MapPost("/{activityId:guid}/sub-activities", async (Guid groupId, Guid activityId, CreateActivityRequest req, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.CreateSubActivityAsync(activityId, req, callerId, ct);
            return Results.Created($"/groups/{groupId}/activities/{detail.Id}", detail);
        });

        // POST /groups/{groupId}/activities/{activityId}/participants — add participant
        grp.MapPost("/{activityId:guid}/participants", async (Guid groupId, Guid activityId, AddParticipantRequest req, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.AddParticipantAsync(activityId, req, callerId, ct);
            return Results.Ok(detail);
        });

        // DELETE /groups/{groupId}/activities/{activityId}/participants/{memberId} — remove participant
        grp.MapDelete("/{activityId:guid}/participants/{memberId:guid}", async (Guid groupId, Guid activityId, Guid memberId, ActivityService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var detail = await svc.RemoveParticipantAsync(activityId, memberId, callerId, ct);
            return Results.Ok(detail);
        });

        return app;
    }
}
