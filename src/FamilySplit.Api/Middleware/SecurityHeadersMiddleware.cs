namespace FamilySplit.Api.Middleware;

/// <summary>
/// Adds a standard set of HTTP security headers to every response.
/// Placed early in the pipeline so the headers are present even on
/// error responses produced by later middleware.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME-type sniffing — browsers must honour the declared Content-Type.
        headers["X-Content-Type-Options"] = "nosniff";

        // Deny framing entirely. This API never serves pages that should be embedded.
        headers["X-Frame-Options"] = "DENY";

        // Limit referrer information sent to third parties.
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Disable browser features that this API never uses.
        headers["Permissions-Policy"] =
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), " +
            "magnetometer=(), microphone=(), payment=(), usb=()";

        // API-appropriate CSP: no HTML is served, so block everything except
        // the bare minimum (same-origin fetch for health/OpenAPI). Scripts,
        // objects, frames, and forms are all denied.
        headers["Content-Security-Policy"] =
            "default-src 'none'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'";

        // Prevent Internet Explorer from switching to compatibility mode,
        // which could bypass other security controls.
        headers["X-UA-Compatible"] = "IE=edge";

        await next(context);
    }
}
