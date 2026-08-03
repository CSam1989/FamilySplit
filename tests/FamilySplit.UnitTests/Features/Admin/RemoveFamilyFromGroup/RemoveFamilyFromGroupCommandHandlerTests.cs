using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.RemoveFamilyFromGroup;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.RemoveFamilyFromGroup;

public class RemoveFamilyFromGroupCommandHandlerTests : AdminCommandTestBase
{
    private readonly RemoveFamilyFromGroupCommandHandler _sut;

    public RemoveFamilyFromGroupCommandHandlerTests()
    {
        _sut = new RemoveFamilyFromGroupCommandHandler(Data.Object, NullLogger<RemoveFamilyFromGroupCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, FamilyId, CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.RemoveFamilyFromGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotInGroup_ThrowsValidation()
    {
        Data.Setup(d => d.FamilyInGroupAsync(GroupId, FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, FamilyId, CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*not in the group*");
        Data.Verify(d => d.RemoveFamilyFromGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Valid_RemovesFamily()
    {
        Data.Setup(d => d.FamilyInGroupAsync(GroupId, FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.HandleAsync(GroupId, FamilyId, CallerId, CT);

        Data.Verify(d => d.RemoveFamilyFromGroupAsync(GroupId, FamilyId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
