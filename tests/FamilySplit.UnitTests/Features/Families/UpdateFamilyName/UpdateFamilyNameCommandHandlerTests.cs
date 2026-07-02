using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.UpdateFamilyName;
using FamilySplit.UnitTests.Features.Families;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Families.UpdateFamilyName;

public class UpdateFamilyNameCommandHandlerTests : FamiliesCommandTestBase
{
    private readonly UpdateFamilyNameCommandHandler _sut;

    public UpdateFamilyNameCommandHandlerTests()
    {
        _sut = new UpdateFamilyNameCommandHandler(
            Data.Object, new UpdateFamilyNameCommandValidator(), NullLogger<UpdateFamilyNameCommandHandler>.Instance);
    }

    private void VerifyNeverUpdated() =>
        Data.Verify(d => d.UpdateFamilyNameAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NoCallerMember_ThrowsForbidden()
    {
        ArrangeNoCallerMember();

        await ((Func<Task>)(() => _sut.HandleAsync(new UpdateFamilyNameCommand("New"), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_NotAdmin_ThrowsForbidden()
    {
        ArrangeCaller(isAdmin: false);

        await ((Func<Task>)(() => _sut.HandleAsync(new UpdateFamilyNameCommand("New"), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidation()
    {
        await ((Func<Task>)(() => _sut.HandleAsync(new UpdateFamilyNameCommand(""), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_Valid_UpdatesFamilyNameForCallersFamily()
    {
        await _sut.HandleAsync(new UpdateFamilyNameCommand("New Name"), CallerId, CT);

        Data.Verify(d => d.UpdateFamilyNameAsync(FamilyId, "New Name", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Valid_TrimsWhitespace()
    {
        await _sut.HandleAsync(new UpdateFamilyNameCommand("  Trimmed  "), CallerId, CT);

        Data.Verify(d => d.UpdateFamilyNameAsync(FamilyId, "Trimmed", It.IsAny<CancellationToken>()), Times.Once);
    }
}
