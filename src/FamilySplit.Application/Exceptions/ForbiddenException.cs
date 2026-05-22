namespace FamilySplit.Application.Exceptions;

/// <summary>
/// Thrown by service methods when the caller lacks the required role.
/// Maps to HTTP 403 via the API's middleware.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to perform this action.") { }
    public ForbiddenException(string message) : base(message) { }
}
