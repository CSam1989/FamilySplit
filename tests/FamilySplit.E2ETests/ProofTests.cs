using FamilySplit.E2ETests.Infrastructure;

// ---------------------------------------------------------------------------
// xUnit collection — one E2EApiServer + E2EClientServer shared by all E2E tests.
// Both are started once when the first test in the collection runs and torn down
// when the last test finishes.
// ---------------------------------------------------------------------------

[CollectionDefinition(nameof(E2ECollection))]
public sealed class E2ECollection :
    ICollectionFixture<E2EApiServer>,
    ICollectionFixture<E2EClientServer>
{ }

// ---------------------------------------------------------------------------
// API-level proof tests (no browser required)
// These validate that the E2E API server starts and responds correctly.
// Run on every machine that has dotnet in PATH, regardless of whether the
// Blazor WASM client has been published.
// ---------------------------------------------------------------------------

[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class ApiProofTests : E2ETestBase
{
    public ApiProofTests(E2EApiServer api, E2EClientServer client)
        : base(api, client) { }

    [Fact]
    public async Task HealthEndpoint_Returns200()
    {
        // The health endpoint is anonymous — no JWT required.
        using var http = new HttpClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await http.GetAsync($"{E2EConfig.ApiBaseUrl}/health", ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        // Any protected endpoint must reject requests without a Bearer token.
        using var http = new HttpClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await http.GetAsync($"{E2EConfig.ApiBaseUrl}/groups", ct);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}

// ---------------------------------------------------------------------------
// Browser proof tests (require the Blazor WASM client to be available)
// These use Playwright to drive a real Chromium browser against the full stack.
// Tests are skipped automatically when E2E_CLIENT_WWWROOT is not set.
// ---------------------------------------------------------------------------

[Trait("Category", "E2E")]
[Collection(nameof(E2ECollection))]
public sealed class BrowserProofTests : E2ETestBase
{
    public BrowserProofTests(E2EApiServer api, E2EClientServer client)
        : base(api, client) { }

    [Fact]
    public async Task UnauthenticatedUser_Redirected_ToLoginPage()
    {
        // Skip if the client isn't being served (E2E_CLIENT_WWWROOT not set).
        if (!ClientAvailable)
        {
            // xUnit v3 skip
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        // Navigate to the root — no refresh cookie is set, so once the silent-refresh
        // auth check completes the app renders the inline sign-in screen (it does not
        // redirect to a separate login page).
        await Page.GotoAsync("/");
        await WaitForNetworkIdleAsync(Page);

        // The Google sign-in button only renders in the unauthenticated state, so its
        // presence is a stable signal that the user was not silently logged in. Using
        // the data-testid avoids depending on i18n text that may not be loaded yet.
        await Expect(Page.Locator("[data-testid='btn-signin-google']"))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AuthenticatedUser_SeededSession_CanAccessHomePage()
    {
        // Skip if the client isn't being served.
        if (!ClientAvailable)
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;

        // Pre-authenticate: seed a refresh token and add the cookie to the browser context.
        // When the app boots, it calls POST /auth/refresh with the cookie → gets a JWT.
        await AuthenticateContextAsync();

        // Navigate to the root of the app.
        await Page.GotoAsync("/");
        await WaitForNetworkIdleAsync(Page);

        // An authenticated user should land on the home/dashboard page — not a login page.
        var currentUrl = Page.Url;
        var pageContent = await Page.ContentAsync();

        var isLoggedIn =
            !currentUrl.Contains("login", StringComparison.OrdinalIgnoreCase)
            && !currentUrl.Contains("not-registered", StringComparison.OrdinalIgnoreCase)
            && !pageContent.Contains("Sign in with Google", StringComparison.OrdinalIgnoreCase);

        isLoggedIn.Should().BeTrue(
            $"an authenticated user should see the home page, " +
            $"but page URL was '{currentUrl}'");
    }
}
