using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.AddParticipant;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.AddParticipant;

public class AddParticipantCommandHandlerTests : ActivityCommandTestBase
{
    private readonly AddParticipantCommandHandler _sut;
    private readonly Guid _memberId = Guid.NewGuid();

    public AddParticipantCommandHandlerTests()
    {
        _sut = new AddParticipantCommandHandler(
            Data.Object, new AddParticipantCommandValidator(), Guard.Object,
            NullLogger<AddParticipantCommandHandler>.Instance);
    }

    private AddParticipantCommand MakeCommand() => new(_memberId);

    /// <summary>Arrange the member-in-group / not-already-participant reads for the happy path.</summary>
    private void ArrangeAddable()
    {
        Data.Setup(d => d.IsMemberInGroupAsync(GroupId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Data.Setup(d => d.IsParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    [Fact]
    public async Task Handle_Valid_AddsParticipant()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeAddable();

        await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        Data.Verify(d => d.AddParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyMemberId_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, new AddParticipantCommand(Guid.Empty), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ClosedActivity_Throws422OnStatus()
    {
        ArrangeActivity(ActivityStatus.Closed);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MemberNotInGroup_Throws422OnFamilyMemberId()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.IsMemberInGroupAsync(GroupId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyParticipant_Throws422OnFamilyMemberId()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.IsMemberInGroupAsync(GroupId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Data.Setup(d => d.IsParticipantAsync(ActivityId, _memberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "FamilyMemberId");
        Data.Verify(d => d.AddParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
