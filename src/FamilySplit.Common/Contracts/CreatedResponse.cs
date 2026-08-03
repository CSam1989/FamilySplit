namespace FamilySplit.Common.Contracts;

/// <summary>
/// The response body for create commands: <c>201 Created</c> + <c>Location</c> header + this body.
/// </summary>
public sealed record CreatedResponse(Guid Id);
