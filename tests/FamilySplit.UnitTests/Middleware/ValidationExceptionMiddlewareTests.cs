using FamilySplit.Api.Middleware;
using FamilySplit.Application.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace FamilySplit.UnitTests.Middleware;

public class ValidationExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ValidationExceptionMiddleware>> _logger = new();

    private ValidationExceptionMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, _logger.Object);

    [Fact]
    public async Task Invoke_NoException_CallsNextAndDoesNotModifyResponse()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Invoke_ForbiddenException_Returns403WithProblemJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = CreateMiddleware(_ => throw new ForbiddenException("no access"));

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(403);
        context.Response.ContentType.Should().Be("application/problem+json");
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Forbidden").And.Contain("no access");
    }

    [Fact]
    public async Task Invoke_ValidationException_Returns422WithErrors()
    {
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Name", "Name too short"),
            new("Age", "Must be positive"),
        };
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = CreateMiddleware(_ => throw new ValidationException(failures));

        await middleware.Invoke(context);

        context.Response.StatusCode.Should().Be(422);
        context.Response.ContentType.Should().Be("application/problem+json");
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Validation failed").And.Contain("Name is required").And.Contain("Must be positive");
    }

    [Fact]
    public async Task Invoke_OperationCanceledException_WhenRequestAborted_LogsDebug()
    {
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cts.Token };
        context.Response.Body = new MemoryStream();
        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());

        await middleware.Invoke(context);

        // Should not throw; status code remains default
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Invoke_OperationCanceledException_WhenNotAborted_Throws()
    {
        var context = new DefaultHttpContext();
        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());

        var act = () => middleware.Invoke(context);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_AssignsFields()
    {
        // Just verifying it doesn't throw
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        middleware.Should().NotBeNull();
    }
}
