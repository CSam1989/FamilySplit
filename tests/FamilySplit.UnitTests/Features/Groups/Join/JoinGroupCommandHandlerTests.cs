using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Join;
using FamilySplit.UnitTests.Features.Groups;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups.Join;

public class JoinGroupCommandHandlerTests : GroupCommandTestBase
{
    private readonly JoinGroupCommandHandler _sut;

    private GroupFamily? _addedMembership;

    public JoinGroupCommandHandlerTests()
    {
        _sut = new JoinGroupCommandHandler(
            Data.Object, new JoinGroupCommandValidator(), Guard.Object,
            NullLogger<JoinGroupCommandHandler>.Instance);

        Data.Setup(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()))
            .Callback<GroupFamily, CancellationToken>((m, _) => _addedMembership = m)
            .Returns(Task.CompletedTask);
    }

    private static JoinGroupCommand MakeCommand(string inviteCode = "ABCD1234") => new(inviteCode);

    [Fact]
    public async Task Handle_ValidCode_JoinsGroupAndReturnsGroupId()
    {
        ArrangeCallerIsFamilyAdmin();
        Data.Setup(d => d.GetGroupIdByInviteCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroupId);
        Data.Setup(d => d.IsFamilyInGroupAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var id = await _sut.HandleAsync(MakeCommand(), CallerId, CT);

        id.Should().Be(GroupId);
        _addedMembership.Should().NotBeNull();
        _addedMembership!.GroupId.Should().Be(GroupId);
        _addedMembership.FamilyId.Should().Be(CallerFamilyId);
        _addedMembership.Role.Should().Be(MemberRole.Member);
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_LowercaseCode_MatchesCaseInsensitively()
    {
        ArrangeCallerIsFamilyAdmin();
        // The handler upper-cases the code before lookup.
        Data.Setup(d => d.GetGroupIdByInviteCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroupId);
        Data.Setup(d => d.IsFamilyInGroupAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var id = await _sut.HandleAsync(MakeCommand("abcd1234"), CallerId, CT);

        id.Should().Be(GroupId);
        Data.Verify(d => d.GetGroupIdByInviteCodeAsync("ABCD1234", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFamilyAdmin_ThrowsForbiddenException()
    {
        ArrangeCallerIsFamilyAdmin(false);

        Func<Task> act = () => _sut.HandleAsync(MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidCode_ThrowsValidationOnInviteCodeField()
    {
        ArrangeCallerIsFamilyAdmin();
        Data.Setup(d => d.GetGroupIdByInviteCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        Func<Task> act = () => _sut.HandleAsync(MakeCommand(), CallerId, CT);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == "InviteCode"
            && e.ErrorMessage == "Invite code is invalid or has expired.");
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyMember_ThrowsValidationOnInviteCodeField()
    {
        ArrangeCallerIsFamilyAdmin();
        Data.Setup(d => d.GetGroupIdByInviteCodeAsync("ABCD1234", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GroupId);
        Data.Setup(d => d.IsFamilyInGroupAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Func<Task> act = () => _sut.HandleAsync(MakeCommand(), CallerId, CT);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == "InviteCode"
            && e.ErrorMessage == "Your family is already a member of this group.");
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Validation runs before any data access — no admin/code setup needed.
        Func<Task> act = () => _sut.HandleAsync(MakeCommand("SHORT"), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
