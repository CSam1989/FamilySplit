using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.RegenerateInviteCode;
using FamilySplit.UnitTests.Features.Groups;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups.RegenerateInviteCode;

public class RegenerateInviteCodeCommandHandlerTests : GroupCommandTestBase
{
    private readonly RegenerateInviteCodeCommandHandler _sut;

    public RegenerateInviteCodeCommandHandlerTests()
    {
        _sut = new RegenerateInviteCodeCommandHandler(
            Data.Object, Guard.Object, NullLogger<RegenerateInviteCodeCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_AsAdmin_GeneratesAndPersistsNewCode()
    {
        ArrangeCallerRoleInGroup(MemberRole.Admin);

        await _sut.HandleAsync(GroupId, CallerId, CT);

        Data.Verify(d => d.GenerateUniqueInviteCodeAsync(It.IsAny<CancellationToken>()), Times.Once);
        Data.Verify(d => d.UpdateInviteCodeAsync(GroupId, "NEWCODE8", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AsMember_ThrowsForbiddenException()
    {
        ArrangeCallerRoleInGroup(MemberRole.Member);

        Func<Task> act = () => _sut.HandleAsync(GroupId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.UpdateInviteCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerFamilyNotInGroup_ThrowsForbiddenException()
    {
        ArrangeCallerRoleInGroup(null);

        Func<Task> act = () => _sut.HandleAsync(GroupId, CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.UpdateInviteCodeAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
