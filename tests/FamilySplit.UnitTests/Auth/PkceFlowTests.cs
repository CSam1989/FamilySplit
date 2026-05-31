using FamilySplit.Api.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Moq;

namespace FamilySplit.UnitTests.Auth;

public class PkceFlowTests
{
    private readonly PkceFlow _sut;

    public PkceFlowTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        _sut = new PkceFlow(provider);
    }

    [Fact]
    public void Constructor_CreatesProtector()
    {
        var mockProvider = new Mock<IDataProtectionProvider>();
        mockProvider.Setup(p => p.CreateProtector("FamilySplit.OAuth.v1"))
            .Returns(new EphemeralDataProtectionProvider().CreateProtector("x"));

        var sut = new PkceFlow(mockProvider.Object);

        mockProvider.Verify(p => p.CreateProtector("FamilySplit.OAuth.v1"), Times.Once);
    }

    [Fact]
    public void NewFlow_ReturnsStateWithReturnUrl()
    {
        var result = _sut.NewFlow("/home");

        result.ReturnUrl.Should().Be("/home");
        result.State.Should().NotBeNullOrWhiteSpace();
        result.CodeVerifier.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NewFlow_GeneratesUniqueValues()
    {
        var a = _sut.NewFlow("/a");
        var b = _sut.NewFlow("/b");

        a.State.Should().NotBe(b.State);
        a.CodeVerifier.Should().NotBe(b.CodeVerifier);
    }

    [Fact]
    public void DeriveCodeChallenge_ReturnsDeterministicHash()
    {
        var result1 = _sut.DeriveCodeChallenge("test-verifier");
        var result2 = _sut.DeriveCodeChallenge("test-verifier");

        result1.Should().Be(result2);
        result1.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DeriveCodeChallenge_DifferentInputs_DifferentOutputs()
    {
        var a = _sut.DeriveCodeChallenge("verifier-a");
        var b = _sut.DeriveCodeChallenge("verifier-b");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Protect_Unprotect_Roundtrip()
    {
        var state = new OAuthFlowState("s", "cv", "/return");

        var encrypted = _sut.Protect(state);
        var result = _sut.Unprotect(encrypted);

        result.Should().Be(state);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unprotect_NullOrWhitespace_ReturnsNull(string? input)
    {
        _sut.Unprotect(input).Should().BeNull();
    }

    [Fact]
    public void Unprotect_TamperedPayload_ReturnsNull()
    {
        _sut.Unprotect("not-a-valid-protected-payload").Should().BeNull();
    }

    [Fact]
    public void Unprotect_InsufficientParts_ReturnsNull()
    {
        // Protect a payload that has fewer than 3 parts by using a mock
        var mockProtector = new Mock<IDataProtector>();
        mockProtector.Setup(p => p.Unprotect(It.IsAny<byte[]>()))
            .Returns(System.Text.Encoding.UTF8.GetBytes("only-one-part"));

        var mockProvider = new Mock<IDataProtectionProvider>();
        mockProvider.Setup(p => p.CreateProtector(It.IsAny<string>()))
            .Returns(mockProtector.Object);

        var sut = new PkceFlow(mockProvider.Object);
        sut.Unprotect("anything").Should().BeNull();
    }

    [Fact]
    public void Protect_PreservesReturnUrlWithPipe()
    {
        var state = new OAuthFlowState("s", "cv", "/return|with|pipes");

        var encrypted = _sut.Protect(state);
        var result = _sut.Unprotect(encrypted);

        result!.ReturnUrl.Should().Be("/return|with|pipes");
    }
}
