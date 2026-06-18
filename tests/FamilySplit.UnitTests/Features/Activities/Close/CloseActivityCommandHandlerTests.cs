using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Close;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.Close;

public class CloseActivityCommandHandlerTests : ActivityCommandTestBase
{
    private readonly CloseActivityCommandHandler _sut;

    public CloseActivityCommandHandlerTests()
    {
        _sut = new CloseActivityCommandHandler(
            Data.Object, Guard.Object, NullLogger<CloseActivityCommandHandler>.Instance);

        Data.Setup(d => d.CloseActivityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    [Fact]
    public async Task Handle_ValidTopLevelOpen_ClosesActivity()
    {
        ArrangeActivity(ActivityStatus.Open);

        await _sut.HandleAsync(ActivityId, CallerId, CT);

        Data.Verify(d => d.CloseActivityAsync(ActivityId, CallerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.CloseActivityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.CloseActivityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ActivityStatus.Closed)]
    [InlineData(ActivityStatus.Settled)]
    [InlineData(ActivityStatus.AbsorbedByParent)]
    public async Task Handle_AlreadyClosedOrSettled_Throws422OnStatus(ActivityStatus status)
    {
        ArrangeActivity(status);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.CloseActivityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SubActivity_Throws422OnStatus()
    {
        ArrangeActivity(ActivityStatus.Open, parentId: Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.CloseActivityAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
