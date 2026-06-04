using FamilySplit.Client.Services;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Moq;

namespace FamilySplit.Client.UnitTests.Services;

public class IGroupClientTests
{
    private readonly Mock<IGroupClient> _mock = new(MockBehavior.Strict);

    private static GroupDetailDto MakeDetail(Guid? id = null) => new(
        id ?? Guid.NewGuid(), "G1", "desc", "INV123", MemberRole.Admin,
        [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public async Task ListAsync_ReturnsExpectedList()
    {
        var expected = new List<GroupSummaryDto>
        {
            new(Guid.NewGuid(), "G1", null, "ABC", 2, MemberRole.Admin, DateTimeOffset.UtcNow),
        };
        _mock.Setup(c => c.ListAsync()).ReturnsAsync(expected);

        var result = await _mock.Object.ListAsync();

        result.Should().BeEquivalentTo(expected);
        _mock.Verify(c => c.ListAsync(), Times.Once);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyList()
    {
        _mock.Setup(c => c.ListAsync()).ReturnsAsync([]);

        var result = await _mock.Object.ListAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_ReturnsGroup()
    {
        var groupId = Guid.NewGuid();
        var expected = MakeDetail(groupId);
        _mock.Setup(c => c.GetAsync(groupId)).ReturnsAsync(expected);

        var result = await _mock.Object.GetAsync(groupId);

        result.Should().Be(expected);
        _mock.Verify(c => c.GetAsync(groupId), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ReturnsCreatedGroup()
    {
        var request = new CreateGroupRequest("New", "desc");
        var expected = MakeDetail();
        _mock.Setup(c => c.CreateAsync(request)).ReturnsAsync(expected);

        var result = await _mock.Object.CreateAsync(request);

        result.Should().Be(expected);
        _mock.Verify(c => c.CreateAsync(request), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedGroup()
    {
        var groupId = Guid.NewGuid();
        var request = new UpdateGroupRequest("Updated", null);
        var expected = MakeDetail(groupId);
        _mock.Setup(c => c.UpdateAsync(groupId, request)).ReturnsAsync(expected);

        var result = await _mock.Object.UpdateAsync(groupId, request);

        result.Should().Be(expected);
        _mock.Verify(c => c.UpdateAsync(groupId, request), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_ReturnsJoinedGroup()
    {
        var request = new JoinGroupRequest("INVITE");
        var expected = MakeDetail();
        _mock.Setup(c => c.JoinAsync(request)).ReturnsAsync(expected);

        var result = await _mock.Object.JoinAsync(request);

        result.Should().Be(expected);
        _mock.Verify(c => c.JoinAsync(request), Times.Once);
    }

    [Fact]
    public async Task RegenerateInviteCodeAsync_ReturnsNewInviteCode()
    {
        var groupId = Guid.NewGuid();
        var expected = new RegenerateInviteCodeResponse("NEWINVITE");
        _mock.Setup(c => c.RegenerateInviteCodeAsync(groupId)).ReturnsAsync(expected);

        var result = await _mock.Object.RegenerateInviteCodeAsync(groupId);

        result.Should().Be(expected);
        _mock.Verify(c => c.RegenerateInviteCodeAsync(groupId), Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_CompletesSuccessfully()
    {
        var groupId = Guid.NewGuid();
        _mock.Setup(c => c.LeaveAsync(groupId)).Returns(Task.CompletedTask);

        await _mock.Object.LeaveAsync(groupId);

        _mock.Verify(c => c.LeaveAsync(groupId), Times.Once);
    }
}
