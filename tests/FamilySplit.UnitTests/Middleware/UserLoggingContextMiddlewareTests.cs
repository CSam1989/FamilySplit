using System.Security.Claims;
using FamilySplit.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FamilySplit.UnitTests.Middleware;

public class UserLoggingContextMiddlewareTests
{
    private readonly UserLoggingContextMiddleware _sut;
    private bool _nextCalled;

    public UserLoggingContextMiddlewareTests()
    {
        _sut = new UserLoggingContextMiddleware(NextDelegate);
    }

    private Task NextDelegate(HttpContext context)
    {
        _nextCalled = true;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_CallsNextWithoutEnriching()
    {
        var context = new DefaultHttpContext();

        await _sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithNameIdentifier_CallsNext()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-123"),
                new Claim(ClaimTypes.Email, "test@example.com"),
            ],
            "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        await _sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithSubClaim_CallsNext()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim("sub", "user-456")],
            "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        await _sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithEmailClaim_CallsNext()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim("sub", "user-789"), new Claim("email", "alt@example.com")],
            "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        await _sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithNoClaims_CallsNext()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity([], "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        await _sut.InvokeAsync(context);

        _nextCalled.Should().BeTrue();
    }
}
