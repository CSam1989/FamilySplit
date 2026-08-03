using FamilySplit.Common.Exceptions;
using FamilySplit.Domain.Entities;
using FamilySplit.Domain.Enums;
using FamilySplit.Features.Groups.Create;
using FamilySplit.UnitTests.Features.Groups;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FamilySplit.UnitTests.Features.Groups.Create;

public class CreateGroupCommandHandlerTests : GroupCommandTestBase
{
    private readonly CreateGroupCommandHandler _sut;

    // Captured arguments from the data gateway's persist call.
    private Group? _addedGroup;
    private GroupFamily? _addedMembership;

    public CreateGroupCommandHandlerTests()
    {
        _sut = new CreateGroupCommandHandler(
            Data.Object, new CreateGroupCommandValidator(), Guard.Object,
            NullLogger<CreateGroupCommandHandler>.Instance);

        Data.Setup(d => d.AddGroupAsync(It.IsAny<Group>(), It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()))
            .Callback<Group, GroupFamily, CancellationToken>((g, m, _) => { _addedGroup = g; _addedMembership = m; })
            .Returns(Task.CompletedTask);
    }

    private static CreateGroupCommand MakeCommand(string name = "New Group", string? description = "A description")
        => new(name, description);

    [Fact]
    public async Task Handle_ValidRequest_CreatesGroupAndReturnsId()
    {
        ArrangeCallerIsFamilyAdmin();

        var id = await _sut.HandleAsync(MakeCommand(), CallerId, CT);

        id.Should().NotBeEmpty();
        _addedGroup.Should().NotBeNull();
        id.Should().Be(_addedGroup!.Id);
        _addedGroup.Name.Should().Be("New Group");
        _addedGroup.Description.Should().Be("A description");
        _addedGroup.InviteCode.Should().Be("NEWCODE8");
        _addedGroup.CreatedByUserId.Should().Be(CallerId);
        Data.Verify(d => d.AddGroupAsync(It.IsAny<Group>(), It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidRequest_AddsCallerFamilyAsAdminMembership()
    {
        ArrangeCallerIsFamilyAdmin();

        var id = await _sut.HandleAsync(MakeCommand(), CallerId, CT);

        _addedMembership.Should().NotBeNull();
        _addedMembership!.GroupId.Should().Be(id);
        _addedMembership.FamilyId.Should().Be(CallerFamilyId);
        _addedMembership.Role.Should().Be(MemberRole.Admin);
    }

    [Fact]
    public async Task Handle_NameAndDescriptionWithWhitespace_GetTrimmed()
    {
        ArrangeCallerIsFamilyAdmin();

        await _sut.HandleAsync(MakeCommand("  Trimmed  ", "  Desc  "), CallerId, CT);

        _addedGroup!.Name.Should().Be("Trimmed");
        _addedGroup.Description.Should().Be("Desc");
    }

    [Fact]
    public async Task Handle_NullDescription_Allowed()
    {
        ArrangeCallerIsFamilyAdmin();

        await _sut.HandleAsync(MakeCommand(description: null), CallerId, CT);

        _addedGroup!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NotFamilyAdmin_ThrowsForbiddenException()
    {
        ArrangeCallerIsFamilyAdmin(false);

        Func<Task> act = () => _sut.HandleAsync(MakeCommand(), CallerId, CT);

        await act.Should().ThrowAsync<ForbiddenException>();
        Data.Verify(d => d.AddGroupAsync(It.IsAny<Group>(), It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        // Validation runs before any data access — no admin setup needed.
        Func<Task> act = () => _sut.HandleAsync(MakeCommand(name: ""), CallerId, CT);

        await act.Should().ThrowAsync<ValidationException>();
        Data.Verify(d => d.AddGroupAsync(It.IsAny<Group>(), It.IsAny<GroupFamily>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
