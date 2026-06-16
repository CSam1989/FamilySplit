using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Data;
using FamilySplit.Features.Groups.Leave;
using FamilySplit.UnitTests.Features.Groups;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups.Leave;

public class LeaveGroupCommandHandlerTests : GroupCommandTestBase
{
    private readonly LeaveGroupCommandHandler _sut;

    public LeaveGroupCommandHandlerTests()
    {
        _sut = new LeaveGroupCommandHandler(
            Data.Object, Guard.Object, NullLogger<LeaveGroupCommandHandler>.Instance);
    }

    private void ArrangeMembership(MemberRole role, Guid? groupFamilyId = null) =>
        Data.Setup(d => d.GetFamilyMembershipAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMembershipInfo(groupFamilyId ?? Guid.NewGuid(), role));

    [Fact]
    public async Task Handle_MemberRole_RemovesFamilyFromGroup()
    {
        ArrangeCallerIsFamilyAdmin();
        var groupFamilyId = Guid.NewGuid();
        ArrangeMembership(MemberRole.Member, groupFamilyId);

        await _sut.HandleAsync(GroupId, CallerId, CT);

        Data.Verify(d => d.RemoveFamilyFromGroupAsync(groupFamilyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminWithOtherAdmins_RemovesFamilyFromGroup()
    {
        ArrangeCallerIsFamilyAdmin();
        var groupFamilyId = Guid.NewGuid();
        ArrangeMembership(MemberRole.Admin, groupFamilyId);
        Data.Setup(d => d.CountGroupAdminsAsync(GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await _sut.HandleAsync(GroupId, CallerId, CT);

        Data.Verify(d => d.RemoveFamilyFromGroupAsync(groupFamilyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SoleAdmin_ThrowsValidationOnGroupField()
    {
        ArrangeCallerIsFamilyAdmin();
        ArrangeMembership(MemberRole.Admin);
        Data.Setup(d => d.CountGroupAdminsAsync(GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        Func<Task> act = () => _sut.HandleAsync(GroupId, CallerId, CT);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == "Group");
        Data.Verify(d => d.RemoveFamilyFromGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotMemberOfGroup_ThrowsValidationOnGroupField()
    {
        ArrangeCallerIsFamilyAdmin();
        Data.Setup(d => d.GetFamilyMembershipAsync(GroupId, CallerFamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMembershipInfo?)null);

        Func<Task> act = () => _sut.HandleAsync(GroupId, CallerId, CT);

        var ex = (await act.Should().ThrowAsync<ValidationException>()).Which;
        ex.Errors.Should().Contain(e => e.PropertyName == "Group"
            && e.ErrorMessage == "Your family is not a member of this group.");
        Data.Verify(d => d.RemoveFamilyFromGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NotFamilyAdmin_ThrowsForbiddenException()
    {
        ArrangeCallerIsFamilyAdmin(false);

        Func<Task> act = () => _sut.HandleAsync(GroupId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.RemoveFamilyFromGroupAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
