using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Auth;
using FluentAssertions;

namespace FamilySplit.Client.UnitTests.Store.Auth;

public class AuthReducersTests
{
    private static AuthState CreateState(
        bool isLoading = false,
        bool isAuthenticated = true,
        bool isGlobalAdmin = true,
        string? error = "some error",
        WhoAmIResponse? currentUser = null) =>
        new()
        {
            IsLoading = isLoading,
            IsAuthenticated = isAuthenticated,
            IsGlobalAdmin = isGlobalAdmin,
            Error = error,
            CurrentUser = currentUser ?? CreateUser(),
        };

    private static WhoAmIResponse CreateUser(bool isGlobalAdmin = false) =>
        new(Guid.NewGuid(), "test@test.com", "Test", null, "Google", DateTimeOffset.UtcNow, isGlobalAdmin);

    [Fact]
    public void OnCheck_SetsIsLoadingTrueAndClearsError()
    {
        var state = CreateState(isLoading: false, error: "old error");

        var result = AuthReducers.OnCheck(state);

        result.IsLoading.Should().BeTrue();
        result.Error.Should().BeNull();
        result.IsAuthenticated.Should().Be(state.IsAuthenticated);
        result.CurrentUser.Should().Be(state.CurrentUser);
    }

    [Fact]
    public void OnSuccess_SetsAuthenticatedAndUser()
    {
        var user = CreateUser(isGlobalAdmin: true);
        var state = CreateState(isLoading: true, isAuthenticated: false, isGlobalAdmin: false);

        var result = AuthReducers.OnSuccess(state, new CheckAuthSuccessAction(user));

        result.IsLoading.Should().BeFalse();
        result.IsAuthenticated.Should().BeTrue();
        result.IsGlobalAdmin.Should().BeTrue();
        result.CurrentUser.Should().Be(user);
    }

    [Fact]
    public void OnSuccess_NonGlobalAdmin_SetsIsGlobalAdminFalse()
    {
        var user = CreateUser(isGlobalAdmin: false);
        var state = CreateState(isLoading: true, isGlobalAdmin: true);

        var result = AuthReducers.OnSuccess(state, new CheckAuthSuccessAction(user));

        result.IsGlobalAdmin.Should().BeFalse();
    }

    [Fact]
    public void OnNotAuthenticated_ResetsAuthState()
    {
        var state = CreateState(isLoading: true, isAuthenticated: true, isGlobalAdmin: true);

        var result = AuthReducers.OnNotAuthenticated(state);

        result.IsLoading.Should().BeFalse();
        result.IsAuthenticated.Should().BeFalse();
        result.CurrentUser.Should().BeNull();
        result.IsGlobalAdmin.Should().BeFalse();
    }

    [Fact]
    public void OnSignOut_ReturnsDefaultState()
    {
        var state = CreateState(isLoading: true, isAuthenticated: true, isGlobalAdmin: true, error: "err");

        var result = AuthReducers.OnSignOut(state);

        result.Should().BeEquivalentTo(new AuthState());
    }
}
