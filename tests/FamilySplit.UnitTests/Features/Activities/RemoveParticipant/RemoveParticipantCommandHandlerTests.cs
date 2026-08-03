using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.RemoveParticipant;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.RemoveParticipant;

public class RemoveParticipantCommandHandlerTests : ActivityCommandTestBase
{
    private readonly RemoveParticipantCommandHandler _sut;
    private readonly Guid _memberId = Guid.NewGuid();

    public RemoveParticipantCommandHandlerTests()
    {
        _sut = new RemoveParticipantCommandHandler(
            Data.Object, Guard.Object, NullLogger<RemoveParticipantCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Valid_RemovesParticipant()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.IsParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.HandleAsync(ActivityId, _memberId, CallerId, CT);

        Data.Verify(d => d.RemoveParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, _memberId, CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.RemoveParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, _memberId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.RemoveParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ClosedActivity_Throws422OnStatus()
    {
        ArrangeActivity(ActivityStatus.Closed);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, _memberId, CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.RemoveParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotAParticipant_Throws422OnFamilyMemberId()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.IsParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, _memberId, CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
        Data.Verify(d => d.RemoveParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
