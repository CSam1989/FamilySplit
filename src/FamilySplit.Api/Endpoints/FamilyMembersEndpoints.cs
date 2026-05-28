using FamilySplit.Application.Families;

namespace FamilySplit.Api.Endpoints;

/// <summary>
/// Personal profile endpoint — returns the FamilyMember linked to the
/// currently authenticated User.
/// </summary>
public static class FamilyMembersEndpoints
{
    public static WebApplication MapFamilyMemberEndpoints(this WebApplication app)
    {
        var grp = app.MapGroup("/users/me")
            .WithTags("Profile");

        // GET /users/me/profile — caller's own FamilyMember profile
        grp.MapGet("/profile", async (FamilyService svc, HttpContext ctx, CancellationToken ct) =>
        {
            var callerId = ctx.User.GetUserId();
            var profile = await svc.GetMyProfileAsync(callerId, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        return app;
    }
}
