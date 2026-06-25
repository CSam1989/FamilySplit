using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using FamilySplit.Features.Admin.UpdateFamilyMember;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.UpdateFamilyMember;

public class UpdateFamilyMemberCommandHandlerTests : AdminCommandTestBase
{
    private readonly UpdateFamilyMemberCommandHandler _sut;

    public UpdateFamilyMemberCommandHandlerTests()
    {
        _sut = new UpdateFamilyMemberCommandHandler(
            Data.Object, new UpdateFamilyMemberCommandValidator(), NullLogger<UpdateFamilyMemberCommandHandler>.Instance);
    }

    private void ArrangeMember(string? currentEmail) =>
        Data.Setup(d => d.GetActiveMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminMemberRecord(MemberId, FamilyId, currentEmail));

    private static UpdateFamilyMemberCommand Cmd(string? email = null) =>
        new("Updated", email, null, null, IsAdmin: true);

    private void VerifyNeverUpdated() =>
        Data.Verify(d => d.UpdateMemberAsync(It.IsAny<Guid>(), It.IsAny<AdminMemberFields>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_MemberNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetActiveMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminMemberRecord?)null);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd(), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_EmailChangedToConflict_ThrowsValidation()
    {
        ArrangeMember(currentEmail: "old@x.test");
        Data.Setup(d => d.EmailInUseAsync("new@x.test", MemberId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, Cmd("new@x.test"), CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
        VerifyNeverUpdated();
    }

    [Fact]
    public async Task Handle_Valid_UpdatesMemberWithNormalisedFields()
    {
        ArrangeMember(currentEmail: "old@x.test");

        AdminMemberFields? fields = null;
        Data.Setup(d => d.UpdateMemberAsync(MemberId, It.IsAny<AdminMemberFields>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, AdminMemberFields, CancellationToken>((_, f, _) => fields = f)
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(MemberId, Cmd("New@X.test"), CallerId, CT);

        fields.Should().NotBeNull();
        fields!.DisplayName.Should().Be("Updated");
        fields.Email.Should().Be("new@x.test");
        fields.IsAdmin.Should().BeTrue();
    }
}
