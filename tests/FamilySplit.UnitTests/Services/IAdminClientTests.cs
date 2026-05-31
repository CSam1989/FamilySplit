using FamilySplit.Client.Services;
using FamilySplit.Domain.Enums;
using FluentAssertions;
using Moq;

namespace FamilySplit.UnitTests.Services;

public class IAdminClientTests
{
    private readonly Mock<IAdminClient> _mock = new(MockBehavior.Strict);

    private CancellationToken CT => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListFamiliesAsync_ReturnsExpectedList()
    {
        var expected = new List<FamilyDto>
        {
            new(Guid.NewGuid(), "Smith", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        };
        _mock.Setup(c => c.ListFamiliesAsync()).ReturnsAsync(expected);

        var result = await _mock.Object.ListFamiliesAsync();

        result.Should().BeEquivalentTo(expected);
        _mock.Verify(c => c.ListFamiliesAsync(), Times.Once);
    }

    [Fact]
    public async Task ListFamiliesAsync_ReturnsEmptyList()
    {
        _mock.Setup(c => c.ListFamiliesAsync()).ReturnsAsync([]);

        var result = await _mock.Object.ListFamiliesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateFamilyAsync_ReturnsCreatedFamily()
    {
        var request = new CreateFamilyRequest("NewFamily");
        var expected = new FamilyDto(Guid.NewGuid(), "NewFamily", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _mock.Setup(c => c.CreateFamilyAsync(request)).ReturnsAsync(expected);

        var result = await _mock.Object.CreateFamilyAsync(request);

        result.Should().Be(expected);
        _mock.Verify(c => c.CreateFamilyAsync(request), Times.Once);
    }

    [Fact]
    public async Task GetFamilyAsync_ReturnsFamily()
    {
        var familyId = Guid.NewGuid();
        var expected = new FamilyDto(familyId, "Test", [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _mock.Setup(c => c.GetFamilyAsync(familyId)).ReturnsAsync(expected);

        var result = await _mock.Object.GetFamilyAsync(familyId);

        result.Should().Be(expected);
        _mock.Verify(c => c.GetFamilyAsync(familyId), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_ReturnsMember()
    {
        var familyId = Guid.NewGuid();
        var request = new AddFamilyMemberRequest("John", "john@test.com", null, null);
        var expected = new FamilyMemberDto(
            Guid.NewGuid(), "John", "john@test.com", null, null,
            1.0m, WeightTier.Volwassene, true, false, false, DateTimeOffset.UtcNow);
        _mock.Setup(c => c.AddMemberAsync(familyId, request)).ReturnsAsync(expected);

        var result = await _mock.Object.AddMemberAsync(familyId, request);

        result.Should().Be(expected);
        _mock.Verify(c => c.AddMemberAsync(familyId, request), Times.Once);
    }

    [Fact]
    public async Task UpdateMemberAsync_ReturnsMember()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var request = new UpdateFamilyMemberRequest("Jane", null, null, 1.5m);
        var expected = new FamilyMemberDto(
            memberId, "Jane", null, null, 1.5m,
            1.5m, WeightTier.Volwassene, true, false, false, DateTimeOffset.UtcNow);
        _mock.Setup(c => c.UpdateMemberAsync(familyId, memberId, request)).ReturnsAsync(expected);

        var result = await _mock.Object.UpdateMemberAsync(familyId, memberId, request);

        result.Should().Be(expected);
        _mock.Verify(c => c.UpdateMemberAsync(familyId, memberId, request), Times.Once);
    }

    [Fact]
    public async Task RemoveMemberAsync_CallsEndpoint()
    {
        var familyId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _mock.Setup(c => c.RemoveMemberAsync(familyId, memberId)).Returns(Task.CompletedTask);

        await _mock.Object.RemoveMemberAsync(familyId, memberId);

        _mock.Verify(c => c.RemoveMemberAsync(familyId, memberId), Times.Once);
    }

    [Fact]
    public async Task DeleteGroupAsync_CallsEndpoint()
    {
        var groupId = Guid.NewGuid();
        _mock.Setup(c => c.DeleteGroupAsync(groupId)).Returns(Task.CompletedTask);

        await _mock.Object.DeleteGroupAsync(groupId);

        _mock.Verify(c => c.DeleteGroupAsync(groupId), Times.Once);
    }

    [Fact]
    public async Task AddFamilyToGroupAsync_CallsEndpoint()
    {
        var groupId = Guid.NewGuid();
        var request = new AdminAddFamilyToGroupRequest(Guid.NewGuid());
        _mock.Setup(c => c.AddFamilyToGroupAsync(groupId, request)).Returns(Task.CompletedTask);

        await _mock.Object.AddFamilyToGroupAsync(groupId, request);

        _mock.Verify(c => c.AddFamilyToGroupAsync(groupId, request), Times.Once);
    }

    [Fact]
    public async Task RemoveFamilyFromGroupAsync_CallsEndpoint()
    {
        var groupId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        _mock.Setup(c => c.RemoveFamilyFromGroupAsync(groupId, familyId)).Returns(Task.CompletedTask);

        await _mock.Object.RemoveFamilyFromGroupAsync(groupId, familyId);

        _mock.Verify(c => c.RemoveFamilyFromGroupAsync(groupId, familyId), Times.Once);
    }
}
