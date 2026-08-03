using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Activities.Update;
using FamilySplit.UnitTests.Features.Activities;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Activities.Update;

public class UpdateActivityCommandHandlerTests : ActivityCommandTestBase
{
    private readonly UpdateActivityCommandHandler _sut;

    public UpdateActivityCommandHandlerTests()
    {
        _sut = new UpdateActivityCommandHandler(
            Data.Object, new UpdateActivityCommandValidator(), Guard.Object,
            NullLogger<UpdateActivityCommandHandler>.Instance);
    }

    private static UpdateActivityCommand MakeCommand(string name = "Updated", string? description = "NewDesc") =>
        new(name, description);

    [Fact]
    public async Task Handle_Valid_UpdatesActivity()
    {
        ArrangeActivity(ActivityStatus.Open);

        await _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        Data.Verify(d => d.UpdateActivityDetailsAsync(ActivityId, "Updated", "NewDesc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TrimsNameAndDescription()
    {
        ArrangeActivity(ActivityStatus.Open);

        await _sut.HandleAsync(ActivityId, MakeCommand("  Trimmed  ", "  Desc  "), CallerId, CT);

        Data.Verify(d => d.UpdateActivityDetailsAsync(ActivityId, "Trimmed", "Desc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NotFound_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*not found*");
        Data.Verify(d => d.UpdateActivityDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CallerNotMember_ThrowsForbiddenException()
    {
        ArrangeActivity(ActivityStatus.Open);
        ArrangeCallerNotMember();

        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.UpdateActivityDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ActivityStatus.Closed)]
    [InlineData(ActivityStatus.Settled)]
    public async Task Handle_NotOpen_Throws422OnStatus(ActivityStatus status)
    {
        ArrangeActivity(status);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.HandleAsync(ActivityId, MakeCommand(), CallerId, CT));

        ex.Errors.Should().Contain(e => e.PropertyName == "Status");
        Data.Verify(d => d.UpdateActivityDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        Func<Task> act = () => _sut.HandleAsync(ActivityId, MakeCommand(name: ""), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.UpdateActivityDetailsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
