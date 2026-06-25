using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Admin.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Admin.CreateFamily;

/// <summary>
/// Command (business logic) — global-admin creates a new family. Holds no EF — all data access goes
/// through <see cref="IAdminData"/> (ADR-001). Returns the new family id (201).
/// </summary>
public sealed class CreateFamilyCommandHandler
{
    private readonly IAdminData _data;
    private readonly CreateFamilyCommandValidator _validator;
    private readonly ILogger<CreateFamilyCommandHandler> _logger;

    public CreateFamilyCommandHandler(
        IAdminData data,
        CreateFamilyCommandValidator validator,
        ILogger<CreateFamilyCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(CreateFamilyCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        if (!await _data.IsGlobalAdminAsync(callerId, ct))
            throw new ForbiddenException();

        var now = DateTimeOffset.UtcNow;
        var family = new Family
        {
            Id = Guid.NewGuid(),
            Name = cmd.Name.Trim(),
            CreatedByUserId = callerId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _data.AddFamilyAsync(family, ct);

        _logger.LogInformation("Family {FamilyId} created by global admin {UserId}", family.Id, callerId);

        return family.Id;
    }
}
