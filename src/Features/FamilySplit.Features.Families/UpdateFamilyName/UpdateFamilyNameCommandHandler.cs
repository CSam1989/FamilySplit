using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.Data;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace FamilySplit.Features.Families.UpdateFamilyName;

/// <summary>
/// Command (business logic) — a family admin renames their own family. Holds no EF — all data access
/// goes through <see cref="IFamilyData"/> (ADR-001). Returns nothing (204).
/// </summary>
public sealed class UpdateFamilyNameCommandHandler
{
    private readonly IFamilyData _data;
    private readonly UpdateFamilyNameCommandValidator _validator;
    private readonly ILogger<UpdateFamilyNameCommandHandler> _logger;

    public UpdateFamilyNameCommandHandler(
        IFamilyData data,
        UpdateFamilyNameCommandValidator validator,
        ILogger<UpdateFamilyNameCommandHandler> logger)
    {
        _data = data;
        _validator = validator;
        _logger = logger;
    }

    public async Task HandleAsync(UpdateFamilyNameCommand cmd, Guid callerId, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(cmd, ct);

        var caller = await _data.GetCallerMemberAsync(callerId, ct)
            ?? throw new ForbiddenException();
        if (!caller.IsAdmin)
            throw new ForbiddenException();

        await _data.UpdateFamilyNameAsync(caller.FamilyId, cmd.Name.Trim(), ct);

        _logger.LogInformation("Family {FamilyId} renamed by admin {UserId}", caller.FamilyId, callerId);
    }
}
