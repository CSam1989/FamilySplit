using System.Security.Claims;

using FluentAssertions;

namespace FamilySplit.UnitTests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_WithNameIdentifierClaim_ReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, expected.ToString())]));

        var result = principal.GetUserId();

        result.Should().Be(expected);
    }

    [Fact]
    public void GetUserId_WithSubClaim_ReturnsGuid()
    {
        var expected = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", expected.ToString())]));

        var result = principal.GetUserId();

        result.Should().Be(expected);
    }

    [Fact]
    public void GetUserId_WithBothClaims_PrefersNameIdentifier()
    {
        var nameId = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, nameId.ToString()), new Claim("sub", sub.ToString())]));

        var result = principal.GetUserId();

        result.Should().Be(nameId);
    }

    [Fact]
    public void GetUserId_NoClaims_ThrowsUnauthorizedAccessException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var act = () => principal.GetUserId();

        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GetUserId_InvalidGuidFormat_ThrowsFormatException()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "not-a-guid")]));

        var act = () => principal.GetUserId();

        act.Should().Throw<FormatException>();
    }
}
