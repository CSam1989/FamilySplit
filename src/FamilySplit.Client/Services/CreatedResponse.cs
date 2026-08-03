namespace FamilySplit.Client.Services;

/// <summary>
/// Body returned by create commands under the strict-CQRS wire format:
/// <c>201 Created</c> + <c>Location</c> header + <c>{ "id": "&lt;guid&gt;" }</c>.
/// Mirrors the API's <c>FamilySplit.Common.Contracts.CreatedResponse</c> (client
/// DTOs are duplicated by design).
/// </summary>
public record CreatedResponse(Guid Id);
