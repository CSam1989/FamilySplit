using FamilySplit.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FamilySplit.UnitTests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsAllSecurityHeaders()
    {
        var nextCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        headers["X-Frame-Options"].ToString().Should().Be("DENY");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
        headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'none'");
        headers["X-UA-Compatible"].ToString().Should().Be("IE=edge");
        nextCalled.Should().BeTrue();
    }
}
