using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Families.AddMember;
using FamilySplit.UnitTests.Features.Families;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Families.AddMember;

public class AddMemberCommandHandlerTests : FamiliesCommandTestBase
{
    private readonly AddMemberCommandHandler _sut;

    public AddMemberCommandHandlerTests()
    {
        _sut = new AddMemberCommandHandler(
            Data.Object, new AddMemberCommandValidator(), NullLogger<AddMemberCommandHandler>.Instance);
    }

    private static AddMemberCommand Cmd(string? email = null, bool isAdmin = false, decimal? weight = null, DateOnly? dob = null) =>
        new("New Member", email, dob, weight, isAdmin);

    private void VerifyNeverAdded() =>
        Data.Verify(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NoCallerMember_ThrowsForbidden()
    {
        ArrangeNoCallerMember();

        await ((Func<Task>)(() => _sut.HandleAsync(Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_NotAdmin_ThrowsForbidden()
    {
        ArrangeCaller(isAdmin: false);

        await ((Func<Task>)(() => _sut.HandleAsync(Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverAdded();
    }

    [Fact]
    public async Task Handle_EmailInUse_ThrowsValidation()
    {
        Data.Setup(d => d.EmailInUseAsync("dupe@x.test", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.HandleAsync(Cmd("dupe@x.test"), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        VerifyNeverAdded();
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

        var id = await _sut.HandleAsync(Cmd("Jane@X.test"), CallerId, CT);

        persisted.Should().NotBeNull();
        persisted!.FamilyId.Should().Be(FamilyId, "the member is added to the caller's own family");
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

        await _sut.HandleAsync(Cmd(email: null), CallerId, CT);

        persisted!.Email.Should().BeNull();
        persisted.UserId.Should().BeNull();
        Data.Verify(d => d.FindUserIdByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailNoMatchingUser_NotLinked()
    {
        Data.Setup(d => d.FindUserIdByEmailAsync("nobody@test.com", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        FamilyMember? persisted = null;
        Data.Setup(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()))
            .Callback<FamilyMember, CancellationToken>((m, _) => persisted = m)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Cmd("nobody@test.com"), CallerId, CT);

        persisted!.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Valid_SetsWeightOverrideAndDateOfBirth()
    {
        FamilyMember? persisted = null;
        Data.Setup(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()))
            .Callback<FamilyMember, CancellationToken>((m, _) => persisted = m)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Cmd(weight: 0.5m, dob: new DateOnly(2015, 1, 1)), CallerId, CT);

        persisted!.WeightOverride.Should().Be(0.5m);
        persisted.DateOfBirth.Should().Be(new DateOnly(2015, 1, 1));
    }

    [Fact]
    public async Task Handle_Valid_PreservesIsAdminFlag()
    {
        FamilyMember? persisted = null;
        Data.Setup(d => d.AddMemberAsync(It.IsAny<FamilyMember>(), It.IsAny<CancellationToken>()))
            .Callback<FamilyMember, CancellationToken>((m, _) => persisted = m)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(Cmd(isAdmin: true), CallerId, CT);

        persisted!.IsAdmin.Should().BeTrue();
    }
}
