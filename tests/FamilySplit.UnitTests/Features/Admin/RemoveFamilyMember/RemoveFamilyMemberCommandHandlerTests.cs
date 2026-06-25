using FamilySplit.Common.Exceptions;
using FamilySplit.Features.Admin.Data;
using FamilySplit.Features.Admin.RemoveFamilyMember;
using FamilySplit.UnitTests.Features.Admin;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.RemoveFamilyMember;

public class RemoveFamilyMemberCommandHandlerTests : AdminCommandTestBase
{
    private readonly RemoveFamilyMemberCommandHandler _sut;

    public RemoveFamilyMemberCommandHandlerTests()
    {
        _sut = new RemoveFamilyMemberCommandHandler(Data.Object, NullLogger<RemoveFamilyMemberCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, CallerId, CT)))
            .Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.DeactivateMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MemberNotFound_ThrowsValidation()
    {
        Data.Setup(d => d.GetActiveMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AdminMemberRecord?)null);

        await ((Func<Task>)(() => _sut.HandleAsync(MemberId, CallerId, CT)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.DeactivateMemberAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Valid_DeactivatesMember()
    {
        Data.Setup(d => d.GetActiveMemberAsync(MemberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminMemberRecord(MemberId, FamilyId, null));

        await _sut.HandleAsync(MemberId, CallerId, CT);

        Data.Verify(d => d.DeactivateMemberAsync(MemberId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
