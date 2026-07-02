using FamilySplit.Features.Notifications.Subscribe;

namespace FamilySplit.UnitTests.Features.Notifications.Subscribe;

/// <summary>
/// Ports the legacy <c>PushNotificationServiceTests.ValidatePushField</c> coverage (indirectly
/// exercised via <c>SubscribeAsync</c>) into direct validator tests. Error messages must match the
/// legacy behaviour exactly so 422 responses are unchanged.
/// </summary>
public class SubscribeCommandValidatorTests
{
    private readonly SubscribeCommandValidator _sut = new();

    private static SubscribeCommand MakeCommand(
        string endpoint = "https://push.example.com/abc123",
        string p256dh = "p256dh-key",
        string auth = "auth-secret")
        => new(endpoint, p256dh, auth);

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        var result = _sut.Validate(MakeCommand());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyEndpoint_FailsWithMessage(string? endpoint)
    {
        var result = _sut.Validate(MakeCommand(endpoint: endpoint!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Endpoint" &&
            e.ErrorMessage == "Endpoint is required and must be at most 2048 characters.");
    }

    [Fact]
    public void EndpointExceeds2048_FailsWithMessage()
    {
        var longEndpoint = "https://push.example.com/" + new string('a', 2048);
        var result = _sut.Validate(MakeCommand(endpoint: longEndpoint));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Endpoint" &&
            e.ErrorMessage == "Endpoint is required and must be at most 2048 characters.");
    }

    [Theory]
    [InlineData("http://push.example.com/abc")]     // not https
    [InlineData("not-a-url")]                        // not absolute
    [InlineData("ftp://push.example.com/abc")]       // wrong scheme
    public void EndpointNotAbsoluteHttps_FailsWithMessage(string endpoint)
    {
        var result = _sut.Validate(MakeCommand(endpoint: endpoint));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Endpoint" &&
            e.ErrorMessage == "Endpoint must be a valid https URL.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyP256dh_FailsWithMessage(string? p256dh)
    {
        var result = _sut.Validate(MakeCommand(p256dh: p256dh!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "P256dh" &&
            e.ErrorMessage == "P256dh is required and must be at most 512 characters.");
    }

    [Fact]
    public void P256dhExceeds512_FailsWithMessage()
    {
        var result = _sut.Validate(MakeCommand(p256dh: new string('a', 513)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "P256dh" &&
            e.ErrorMessage == "P256dh is required and must be at most 512 characters.");
    }

    [Fact]
    public void P256dhExactly512_PassesValidation()
    {
        var result = _sut.Validate(MakeCommand(p256dh: new string('a', 512)));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyAuth_FailsWithMessage(string? auth)
    {
        var result = _sut.Validate(MakeCommand(auth: auth!));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Auth" &&
            e.ErrorMessage == "Auth is required and must be at most 512 characters.");
    }

    [Fact]
    public void AuthExceeds512_FailsWithMessage()
    {
        var result = _sut.Validate(MakeCommand(auth: new string('a', 513)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Auth" &&
            e.ErrorMessage == "Auth is required and must be at most 512 characters.");
    }

    [Fact]
    public void AuthExactly512_PassesValidation()
    {
        var result = _sut.Validate(MakeCommand(auth: new string('a', 512)));
        result.IsValid.Should().BeTrue();
    }
}
