using FamilySplit.Api.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;

namespace FamilySplit.UnitTests.Endpoints;

public class GroupMembersEndpointsTests
{
    [Fact]
    public void MapGroupMemberEndpoints_ReturnsSameApp()
    {
        // Arrange
        var app = WebApplication.CreateBuilder().Build();

        // Act
        var result = app.MapGroupMemberEndpoints();

        // Assert
        result.Should().BeSameAs(app);
    }
}
