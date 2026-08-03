using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Create;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.Create;

public class CreateActivityCommandHandlerTests : ActivityCommandTestBase
{
    private readonly CreateActivityCommandHandler _sut;
    private Activity? _persisted;
    private IReadOnlyList<ActivityParticipant>? _persistedParticipants;

    public CreateActivityCommandHandlerTests()
    {
        _sut = new CreateActivityCommandHandler(
            Data.Object, new CreateActivityCommandValidator(), Guard.Object,
            NullLogger<CreateActivityCommandHandler>.Instance);

        Data.Setup(d => d.PersistNewActivityAsync(
                It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()))
            .Callback<Activity, IReadOnlyList<ActivityParticipant>, CancellationToken>((a, ps, _) =>
            {
                _persisted = a;
                _persistedParticipants = ps;
            })
            .Returns(Task.CompletedTask);
    }

    private static CreateActivityCommand MakeCommand(string name = "Beach Trip", string? description = "Summer") =>
        new(name, description);

    [Fact]
    public async Task Handle_Valid_ReturnsNewIdAndPersistsOpenActivity()
    {
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        Data.Setup(d => d.GetActiveGroupMemberIdsAsync(GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([memberA, memberB]);

        var id = await _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        id.Should().NotBeEmpty();
        Data.Verify(d => d.PersistNewActivityAsync(
            It.Is<Activity>(a => a.Id == id), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Once);
        _persisted!.GroupId.Should().Be(GroupId);
        _persisted.Status.Should().Be(ActivityStatus.Open);
        _persisted.CreatedByUserId.Should().Be(CallerId);
    }

    [Fact]
    public async Task Handle_Valid_SeedsParticipantFromEachActiveGroupMember()
    {
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        Data.Setup(d => d.GetActiveGroupMemberIdsAsync(GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([memberA, memberB]);

        var id = await _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        _persistedParticipants.Should().HaveCount(2);
        _persistedParticipants!.Select(p => p.FamilyMemberId).Should().BeEquivalentTo([memberA, memberB]);
        _persistedParticipants.Should().AllSatisfy(p => p.ActivityId.Should().Be(id));
    }

    [Fact]
    public async Task Handle_TrimsNameAndDescription()
    {
        Data.Setup(d => d.GetActiveGroupMemberIdsAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _sut.HandleAsync(GroupId, MakeCommand("  Trimmed  ", "  Desc  "), CallerId, CT);

        _persisted!.Name.Should().Be("Trimmed");
        _persisted.Description.Should().Be("Desc");
    }

    [Fact]
    public async Task Handle_NullDescription_StoredAsNull()
    {
        Data.Setup(d => d.GetActiveGroupMemberIdsAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await _sut.HandleAsync(GroupId, MakeCommand("NoDesc", null), CallerId, CT);

        _persisted!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(GroupId, MakeCommand(name: ""), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.PersistNewActivityAsync(It.IsAny<Activity>(), It.IsAny<IReadOnlyList<ActivityParticipant>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
