using System.Net;
using System.Text.Json;
using Refit;

namespace FamilySplit.Client.Services;

/// <summary>
/// Converts exceptions (especially Refit <see cref="ApiException"/>) into
/// user-friendly messages. Raw HTTP status strings are never shown to the user.
/// </summary>
public static class ErrorHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static string GetMessage(Exception ex)
    {
        if (ex is ApiException api)
            return FromApiException(api);

        return "Something went wrong. Please try again.";
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static string FromApiException(ApiException api)
    {
        // Try to extract structured detail from the response body first.
        if (!string.IsNullOrWhiteSpace(api.Content))
        {
            try
            {
                using var doc = JsonDocument.Parse(api.Content);
                var root = doc.RootElement;

                // 422: { "errors": { "Field": ["msg1", "msg2"] } }
                if (root.TryGetProperty("errors", out var errorsEl))
                {
                    var messages = errorsEl.EnumerateObject()
                        .SelectMany(p => p.Value.EnumerateArray()
                            .Select(v => v.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                        .ToList();

                    if (messages.Count > 0)
                        return string.Join(" ", messages!);
                }

                // 403: { "detail": "..." }
                if (root.TryGetProperty("detail", out var detailEl))
                {
                    var detail = detailEl.GetString();
                    if (!string.IsNullOrWhiteSpace(detail))
                        return detail;
                }
            }
            catch (JsonException)
            {
                // Body wasn't valid JSON — fall through to status-code messages.
            }
        }

        return api.StatusCode switch
        {
            HttpStatusCode.Forbidden           => "You don't have permission to do this.",
            HttpStatusCode.Unauthorized        => "Your session has expired. Please log in again.",
            HttpStatusCode.NotFound            => "The requested item could not be found.",
            HttpStatusCode.UnprocessableEntity => "The request was invalid. Please check your input.",
            HttpStatusCode.Conflict            => "This conflicts with existing data. Please review and try again.",
            HttpStatusCode.InternalServerError => "A server error occurred. Please try again later.",
            HttpStatusCode.ServiceUnavailable  => "The service is temporarily unavailable. Please try again later.",
            _ => "Something went wrong. Please try again."
        };
    }
}
