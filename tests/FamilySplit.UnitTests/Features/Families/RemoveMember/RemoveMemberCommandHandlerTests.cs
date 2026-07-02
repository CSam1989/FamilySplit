using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Families.Data;
using FamilySplit.Features.Families.RemoveMember;
using FamilySplit.UnitTests.Features.Families;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Families.RemoveMember;

public class RemoveMemberCommandHandlerTests : FamiliesCommandTestBase
{
    private readonly RemoveMemberCommandHandler _sut;

    public RemoveMemberCommandHandlerTests()
    {
        _sut = new RemoveMemberCommandHandler(Data.Object, NullLogger<RemoveMemberCommandHandler>.Instance);
    }

    private void ArrangeTarget() =>
        Data.Setup(d => d.GetActiveMemberInFamilyAsync(MemberId, FamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FamilyMemberRecord(MemberId, FamilyId, null, false));

    private void VerifyNeverDeactivated() =>
        Data.Verify(d => d.DeactivateMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task Handle_NoCallerMember_ThrowsForbidden()
    {
        ArrangeNoCallerMember();

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverDeactivated();
    }

    [Fact]
    public async Task Handle_NotAdmin_ThrowsForbidden()
    {
        ArrangeCaller(isAdmin: false);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        VerifyNeverDeactivated();
    }

    [Fact]
    public async Task Handle_AdminRemovesSelf_ThrowsValidation()
    {
        await ((Func<Task>)(() => _sut.HandleAsync(CallerMemberId, CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*cannot remove yourself*");
        VerifyNeverDeactivated();
    }

    [Fact]
    public async Task Handle_MemberNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetActiveMemberInFamilyAsync(MemberId, FamilyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FamilyMemberRecord?)null);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        VerifyNeverDeactivated();
    }

    [Fact]
    public async Task Handle_AdminRemovesOtherMember_Deactivates()
    {
        ArrangeTarget();

        await _sut.HandleAsync(MemberId, CallerId, CT);

        Data.Verify(d => d.DeactivateMemberAsync(MemberId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
