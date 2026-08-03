using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.Data;
using FamilySplit.Features.Families.UpdateMember;
using FamilySplit.UnitTests.Features.Families;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Families.UpdateMember;

public class UpdateMemberCommandHandlerTests : FamiliesCommandTestBase
{
    private readonly UpdateMemberCommandHandler _sut;

    public UpdateMemberCommandHandlerTests()
    {
        _sut = new UpdateMemberCommandHandler(
            Data.Object, new UpdateMemberCommandValidator(), NullLogger<UpdateMemberCommandHandler>.Instance);
    }

    private void ArrangeTarget(string? currentEmail = null, bool currentIsAdmin = false, Guid? memberId = null) =>
        Data.Setup(d => d.GetActiveMemberInFamilyAsync(memberId ?? MemberId, FamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FamilyMemberRecord(memberId ?? MemberId, FamilyId, currentEmail, currentIsAdmin));

    private static UpdateMemberCommand Cmd(string? email = null, bool isAdmin = false) =>
        new("Updated", email, null, null, isAdmin);

    private void VerifyNeverUpdated() =>
        Data.Verify(d => d.UpdateMemberAsync(It.IsAny<Guid>(), It.IsAny<FamilyMemberFields>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NoCallerMember_ThrowsForbidden()
    {
        ArrangeNoCallerMember();

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_NonAdminEditsOther_ThrowsForbidden()
    {
        ArrangeCaller(isAdmin: false);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_NonAdminEditsSelf_Succeeds()
    {
        ArrangeCaller(isAdmin: false);
        ArrangeTarget(currentIsAdmin: false, memberId: CallerMemberId);

        await _sut.HandleAsync(CallerMemberId, Cmd("new@test.com"), CallerId, CT);

        Data.Verify(d => d.UpdateMemberAsync(CallerMemberId, It.IsAny<FamilyMemberFields>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MemberNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetActiveMemberInFamilyAsync(MemberId, FamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FamilyMemberRecord?)null);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_EmailChangedToConflict_ThrowsValidation()
    {
        ArrangeTarget(currentEmail: "old@x.test");
        Data.Setup(d => d.EmailInUseAsync("new@x.test", MemberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd("new@x.test"), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_SameEmail_NoConflictCheck()
    {
        ArrangeTarget(currentEmail: "keep@test.com");

        await _sut.HandleAsync(MemberId, Cmd("keep@test.com"), CallerId, CT);

        Data.Verify(d => d.EmailInUseAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AdminCaller_UpdatesMemberWithNormalisedFieldsAndAdminFlag()
    {
        ArrangeTarget(currentEmail: "old@x.test");

        FamilyMemberFields? fields = null;
        Data.Setup(d => d.UpdateMemberAsync(MemberId, It.IsAny<FamilyMemberFields>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, FamilyMemberFields, CancellationToken>((_, f, _) => fields = f)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(MemberId, Cmd("New@X.test", isAdmin: true), CallerId, CT);

        fields.Should().NotBeNull();
        fields!.DisplayName.Should().Be("Updated");
        fields.Email.Should().Be("new@x.test");
        fields.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonAdminCannotSelfElevate()
    {
        ArrangeCaller(isAdmin: false);
        ArrangeTarget(currentIsAdmin: false, memberId: CallerMemberId);

        FamilyMemberFields? fields = null;
        Data.Setup(d => d.UpdateMemberAsync(CallerMemberId, It.IsAny<FamilyMemberFields>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, FamilyMemberFields, CancellationToken>((_, f, _) => fields = f)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(CallerMemberId, Cmd(isAdmin: true), CallerId, CT);

        fields!.IsAdmin.Should().BeFalse("a non-admin editing themselves cannot self-elevate");
    }

    [Fact]
    public async Task Handle_NullEmail_SetsNull()
    {
        ArrangeTarget(currentEmail: "old@test.com");

        FamilyMemberFields? fields = null;
        Data.Setup(d => d.UpdateMemberAsync(MemberId, It.IsAny<FamilyMemberFields>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, FamilyMemberFields, CancellationToken>((_, f, _) => fields = f)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(MemberId, Cmd(email: null), CallerId, CT);

        fields!.Email.Should().BeNull();
    }
}
