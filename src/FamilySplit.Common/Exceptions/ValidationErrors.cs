using FluentValidation;
using FluentValidation.Results;

namespace FamilySplit.Common.Exceptions;

/// <summary>
/// Factories for the 422 errors handlers throw outside of request validators.
/// The failure is built through a <see cref="ValidationFailure"/> so the message
/// survives the ValidationExceptionMiddleware (which serializes ex.Errors, not
/// ex.Message).
/// </summary>
public static class ValidationErrors
{
    /// <summary>Entity lookup failed — deliberately 422 (not 404), the shape the client expects.</summary>
    public static ValidationException NotFound(string message) =>
        new(new[] { new ValidationFailure("Id", message) });

    /// <summary>Business-rule violation attributed to a specific field.</summary>
    public static ValidationException Field(string field, string message) =>
        new(new[] { new ValidationFailure(field, message) });
}
