using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Create;
using FamilySplit.Features.Activities.CreateSubActivity;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.CreateSubActivity;

public class CreateSubActivityCommandHandlerTests : ActivityCommandTestBase
{
    private readonly CreateSubActivityCommandHandler _sut;
    private Activity? _persisted;
    private IReadOnlyList<ActivityParticipant>? _persistedParticipants;

    public CreateSubActivityCommandHandlerTests()
    {
        _sut = new CreateSubActivityCommandHandler(
            Data.Object, new CreateActivityCommandValidator(), Guard.Object,
            NullLogger<CreateSubActivityCommandHandler>.Instance);

        Data.Setup(d => d.PersistNewActivityAsync(
                It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()))
            .Callback<Activity, IReadOnlyList<ActivityParticipant>, CancellationToken>((a, ps, _) =>
            {
                _persisted = a;
                _persistedParticipants = ps;
            })
            .Returns(Task.CompletedTask);
    }

    private static CreateActivityCommand MakeCommand(string name = "Sub", string? description = null) =>
        new(name, description);

    [Fact]
    public async Task Handle_Valid_ReturnsNewIdAndPersistsSubUnderParent()
    {
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.GetActivityParticipantMemberIdsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var id = await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        id.Should().NotBeEmpty();
        _persisted!.ParentActivityId.Should().Be(ActivityId);
        _persisted.GroupId.Should().Be(GroupId);
        _persisted.Status.Should().Be(ActivityStatus.Open);
    }

    [Fact]
    public async Task Handle_Valid_CopiesParentParticipants()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        ArrangeActivity(ActivityStatus.Open);
        Data.Setup(d => d.GetActivityParticipantMemberIdsAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync([m1, m2]);

        var id = await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        _persistedParticipants!.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([m1, m2]);
        _persistedParticipants.Should().AllSatisfy(p => p.ActivityId.Should().Be(id));
    }

    [Fact]
    public async Task Handle_ParentNotFound_ThrowsValidationException()
    {
        // GetActivityCoreAsync unconfigured → null.
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ParentIsItselfASubActivity_Throws422OnParentActivityId()
    {
        ArrangeActivity(ActivityStatus.Open, parentId: Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "ParentActivityId");
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ActivityStatus.Closed)]
    [InlineData(ActivityStatus.Settled)]
    public async Task Handle_ParentNotOpen_Throws422OnStatus(ActivityStatus status)
    {
        ArrangeActivity(status);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(name: ""), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
