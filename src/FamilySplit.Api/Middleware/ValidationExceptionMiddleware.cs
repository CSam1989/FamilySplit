using System.Text.Json;
using FamilySplit.Common.Exceptions;
using FluentValidation;

namespace FamilySplit.Api.Middleware;

/// <summary>
/// Catches <see cref="ValidationException"/> thrown from service methods
/// (after <c>validator.ValidateAndThrowAsync</c>) and converts them into
/// HTTP 422 with a field-level error map. Also catches <see cref="ForbiddenException"/>
/// (403) and, as a final catch-all, any other unhandled exception (500) — logged in full
/// at Error, with only a generic, non-leaking message returned to the client.
/// </summary>
public class ValidationExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ForbiddenException ex)
        {
            _logger.LogInformation(ex, "Forbidden for {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.3",
                title = "Forbidden",
                status = 403,
                detail = ex.Message
            }, JsonOptions));
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation(ex, "Validation failed for {Path}", context.Request.Path);

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
                title = "Validation failed",
                status = 422,
                errors
            }, JsonOptions));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client closed the connection before the response was sent — nothing to write back.
            _logger.LogDebug("Request to {Path} cancelled by client", context.Request.Path);
        }
        catch (OperationCanceledException)
        {
            // Not caused by client abort (e.g. a genuine timeout) — let it propagate rather
            // than masking it as a generic 500 below.
            throw;
        }
        catch (Exception ex)
        {
            // Final catch-all: anything not already classified above is unexpected. Log the
            // full exception (this is the only app-level place unclassified exceptions are
            // logged) and return a generic, user-friendly body — never the exception message
            // or stack trace.
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                title = "An unexpected error occurred",
                status = 500,
                detail = "Please try again later. If the problem persists, contact support."
            }, JsonOptions));
        }
    }
}
