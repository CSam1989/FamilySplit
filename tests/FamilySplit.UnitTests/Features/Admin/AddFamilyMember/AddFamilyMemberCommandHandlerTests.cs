using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Admin.AddFamilyMember;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.AddFamilyMember;

public class AddFamilyMemberCommandHandlerTests : AdminCommandTestBase
{
    private readonly AddFamilyMemberCommandHandler _sut;

    public AddFamilyMemberCommandHandlerTests()
    {
        _sut = new AddFamilyMemberCommandHandler(
            Data.Object, new AddFamilyMemberCommandValidator(), NullLogger<AddFamilyMemberCommandHandler>.Instance);
        Data.Setup(d => d.FamilyExistsAsync(FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static AddFamilyMemberCommand Cmd(string? email = null) =>
        new("New Member", email, null, null, IsAdmin: false);

    private static void VerifyNeverAdded(Mock<FamilySplit.Features.Admin.Data.IAdminData> data) =>
        data.Verify(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(FamilyId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverAdded(Data);
    }

    [Fact]
    public async Task Handle_FamilyNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.FamilyExistsAsync(FamilyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ((Func<Task>)(() => _sut.HandleAsync(FamilyId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*Family not found*");
        VerifyNeverAdded(Data);
    }

    [Fact]
    public async Task Handle_EmailInUse_ThrowsValidation()
    {
        Data.Setup(d => d.EmailInUseAsync("dupe@x.test", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.HandleAsync(FamilyId, Cmd("dupe@x.test"), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        VerifyNeverAdded(Data);
    }

    [Fact]
    public async Task Handle_ValidWithMatchingUser_AutoLinksAndReturnsId()
    {
        var userId = Guid.NewGuid();
        Data.Setup(d => d.FindUserIdByEmailAsync("jane@x.test", It.IsAny<CancellationToken>())).ReturnsAsync(userId);

        FamilyMember? persisted = null;
        Data.Setup(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()))
            .Callback<FamilyMember, CancellationToken>((m, _) => persisted = m)
            .Returns(Task.CompletedTask);

        var id = await _sut.HandleAsync(FamilyId, Cmd("Jane@X.test"), CallerId, CT);

        persisted.Should().NotBeNull();
        persisted!.FamilyId.Should().Be(FamilyId);
        persisted.Email.Should().Be("jane@x.test", "email is normalised to lowercase");
        persisted.UserId.Should().Be(userId, "a matching User is auto-linked");
        id.Should().Be(persisted.Id);
    }

    [Fact]
    public async Task Handle_ValidNoEmail_PersistsWithoutLink()
    {
        FamilyMember? persisted = null;
        Data.Setup(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()))
            .Callback<FamilyMember, CancellationToken>((m, _) => persisted = m)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(FamilyId, Cmd(email: null), CallerId, CT);

        persisted!.Email.Should().BeNull();
        persisted.UserId.Should().BeNull();
        Data.Verify(d => d.FindUserIdByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
