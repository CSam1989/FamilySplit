using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Update;
using FamilySplit.UnitTests.Features.Groups;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups.Update;

public class UpdateGroupCommandHandlerTests : GroupCommandTestBase
{
    private readonly UpdateGroupCommandHandler _sut;

    public UpdateGroupCommandHandlerTests()
    {
        _sut = new UpdateGroupCommandHandler(
            Data.Object, new UpdateGroupCommandValidator(), Guard.Object,
            NullLogger<UpdateGroupCommandHandler>.Instance);
    }

    private static UpdateGroupCommand MakeCommand(string name = "Updated Name", string? description = "Updated Desc")
        => new(name, description);

    [Fact]
    public async Task Handle_AsAdmin_UpdatesGroup()
    {
        ArrangeCallerRoleInGroup(MemberRole.Admin);

        await _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        Data.Verify(d => d.UpdateGroupDetailsAsync(GroupId, "Updated Name", "Updated Desc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TrimsNameAndDescription()
    {
        ArrangeCallerRoleInGroup(MemberRole.Admin);

        await _sut.HandleAsync(GroupId, MakeCommand("  Trimmed  ", "  D  "), CallerId, CT);

        Data.Verify(d => d.UpdateGroupDetailsAsync(GroupId, "Trimmed", "D", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AsMember_ThrowsForbiddenException()
    {
        ArrangeCallerRoleInGroup(MemberRole.Member);

        Func<Task> act = () => _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.UpdateGroupDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerFamilyNotInGroup_ThrowsForbiddenException()
    {
        ArrangeCallerRoleInGroup(null);

        Func<Task> act = () => _sut.HandleAsync(GroupId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.UpdateGroupDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Validation runs before any data access — no role setup needed.
        Func<Task> act = () => _sut.HandleAsync(GroupId, MakeCommand(name: ""), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.UpdateGroupDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
