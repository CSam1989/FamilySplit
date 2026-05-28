using System.Security.Claims;
using Serilog.Context;

namespace FamilySplit.Api.Middleware;

/// <summary>
/// Enriches every Serilog log entry for authenticated requests with the caller's
/// <c>UserId</c> and <c>UserEmail</c> properties, sourced from the validated JWT claims.
///
/// Must be placed in the pipeline AFTER <c>UseAuthentication()</c> /
/// <c>UseAuthorization()</c> so that <see cref="HttpContext.User"/> is populated.
/// </summary>
public class UserLoggingContextMiddleware
{
    private readonly RequestDelegate _next;

    public UserLoggingContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // sub is the User.Id (Guid).  email is optional — may be absent on some flows.
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub");
        var email = context.User.FindFirstValue(ClaimTypes.Email)
                     ?? context.User.FindFirstValue("email");

        // PushProperty adds the value to every log entry emitted while _next executes.
        // The using block removes the enrichment when the request finishes.
        using var _ = LogContext.PushProperty("UserId", userId);
        using var __ = LogContext.PushProperty("UserEmail", email);

        await _next(context);
    }
}
