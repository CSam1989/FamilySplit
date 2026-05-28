using System.Text.Json;
using FamilySplit.Application.Exceptions;
using FluentValidation;

namespace FamilySplit.Api.Middleware;

/// <summary>
/// Catches <see cref="ValidationException"/> thrown from service methods
/// (after <c>validator.ValidateAndThrowAsync</c>) and converts them into
/// HTTP 422 with a field-level error map.
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
    }
}
