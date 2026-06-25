using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.DeleteGroup;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.DeleteGroup;

public class DeleteGroupCommandHandlerTests : AdminCommandTestBase
{
    private readonly DeleteGroupCommandHandler _sut;

    public DeleteGroupCommandHandlerTests()
    {
        _sut = new DeleteGroupCommandHandler(Data.Object, NullLogger<DeleteGroupCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.DeleteGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GroupNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.DeleteGroupAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*Group not found*");
    }

    [Fact]
    public async Task Handle_Valid_DeletesGroup()
    {
        Data.Setup(d => d.DeleteGroupAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.HandleAsync(GroupId, CallerId, CT);

        Data.Verify(d => d.DeleteGroupAsync(GroupId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
