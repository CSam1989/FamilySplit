using System.Reflection;
using FluentAssertions;
using Refit;

namespace FamilySplit.UnitTests.Services;

public class IPushClientTests
{
    [Fact]
    public void GetVapidPublicKeyAsync_HasGetAttribute_WithCorrectPath()
    {
        var method = typeof(FamilySplit.Client.Services.IPushClient)
            .GetMethod(nameof(FamilySplit.Client.Services.IPushClient.GetVapidPublicKeyAsync));

        var attr = method!.GetCustomAttribute<GetAttribute>();
        attr.Should().NotBeNull();
        attr!.Path.Should().Be("/push/vapid-public-key");
    }

    [Fact]
    public void SubscribeAsync_HasPostAttribute_WithCorrectPath()
    {
        var method = typeof(FamilySplit.Client.Services.IPushClient)
            .GetMethod(nameof(FamilySplit.Client.Services.IPushClient.SubscribeAsync));

        var attr = method!.GetCustomAttribute<PostAttribute>();
        attr.Should().NotBeNull();
        attr!.Path.Should().Be("/push/subscribe");
    }

    [Fact]
    public void SubscribeAsync_ParameterHasBodyAttribute()
    {
        var method = typeof(FamilySplit.Client.Services.IPushClient)
            .GetMethod(nameof(FamilySplit.Client.Services.IPushClient.SubscribeAsync));

        var param = method!.GetParameters()[0];
        param.GetCustomAttribute<BodyAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void UnsubscribeAsync_HasDeleteAttribute_WithCorrectPath()
    {
        var method = typeof(FamilySplit.Client.Services.IPushClient)
            .GetMethod(nameof(FamilySplit.Client.Services.IPushClient.UnsubscribeAsync));

        var attr = method!.GetCustomAttribute<DeleteAttribute>();
        attr.Should().NotBeNull();
        attr!.Path.Should().Be("/push/unsubscribe");
    }

    [Fact]
    public void UnsubscribeAsync_ParameterHasBodyAttribute()
    {
        var method = typeof(FamilySplit.Client.Services.IPushClient)
            .GetMethod(nameof(FamilySplit.Client.Services.IPushClient.UnsubscribeAsync));

        var param = method!.GetParameters()[0];
        param.GetCustomAttribute<BodyAttribute>().Should().NotBeNull();
    }
}
