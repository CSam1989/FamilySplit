using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Features.Admin.CreateFamily;
using FamilySplit.UnitTests.Features.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Admin.CreateFamily;

public class CreateFamilyCommandHandlerTests : AdminCommandTestBase
{
    private readonly CreateFamilyCommandHandler _sut;

    public CreateFamilyCommandHandlerTests()
    {
        _sut = new CreateFamilyCommandHandler(
            Data.Object, new CreateFamilyCommandValidator(), NullLogger<CreateFamilyCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotGlobalAdmin_ThrowsForbidden()
    {
        ArrangeNotGlobalAdmin();

        Func<Task> act = () => _sut.HandleAsync(new CreateFamilyCommand("Smith"), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.AddFamilyAsync(It.IsAny<Family>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Valid_AddsFamilyAndReturnsId()
    {
        Family? persisted = null;
        Data.Setup(d => d.AddFamilyAsync(It.IsAny<Family>(), It.IsAny<CancellationToken>()))
            .Callback<Family, CancellationToken>((f, _) => persisted = f)
            .Returns(Task.CompletedTask);

        var id = await _sut.HandleAsync(new CreateFamilyCommand("  Smith  "), CallerId, CT);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Smith");
        persisted.CreatedByUserId.Should().Be(CallerId);
        id.Should().Be(persisted.Id);
    }
}
