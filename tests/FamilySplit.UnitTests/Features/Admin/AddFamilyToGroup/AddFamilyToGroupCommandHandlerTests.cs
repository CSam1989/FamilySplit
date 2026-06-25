using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Admin.AddFamilyToGroup;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.AddFamilyToGroup;

public class AddFamilyToGroupCommandHandlerTests : AdminCommandTestBase
{
    private readonly AddFamilyToGroupCommandHandler _sut;

    public AddFamilyToGroupCommandHandlerTests()
    {
        _sut = new AddFamilyToGroupCommandHandler(Data.Object, NullLogger<AddFamilyToGroupCommandHandler>.Instance);
        Data.Setup(d => d.GroupExistsAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Data.Setup(d => d.FamilyExistsAsync(FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private AddFamilyToGroupCommand Cmd() => new(FamilyId);

    private void VerifyNeverAdded() =>
        Data.Verify(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_GroupNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GroupExistsAsync(GroupId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*Group not found*");
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_FamilyNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.FamilyExistsAsync(FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*Family not found*");
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_AlreadyInGroup_ThrowsValidation()
    {
        Data.Setup(d => d.FamilyInGroupAsync(GroupId, FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.HandleAsync(GroupId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*already in the group*");
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_Valid_AddsFamilyAsMember()
    {
        GroupFamily? persisted = null;
        Data.Setup(d => d.AddFamilyToGroupAsync(It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()))
            .Callback<GroupFamily, CancellationToken>((gf, _) => persisted = gf)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(GroupId, Cmd(), CallerId, CT);

        persisted.Should().NotBeNull();
        persisted!.GroupId.Should().Be(GroupId);
        persisted.FamilyId.Should().Be(FamilyId);
        persisted.Role.Should().Be(Domain.Enums.MemberRole.Member);
    }
}
