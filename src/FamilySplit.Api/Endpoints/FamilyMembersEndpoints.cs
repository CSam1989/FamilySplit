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
            .RequireAuthorization()
            .WithTags("Profile");

        // GET /users/me/profile — caller's own FamilyMember profile
        grp.MapGet("/profile", async (FamilyService svc, HttpContext ctx) =>
        {
            var callerId = ctx.User.GetUserId();
            var profile = await svc.GetMyProfileAsync(callerId);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        return app;
    }
}
