namespace FamilySplit.E2ETests.Infrastructure;

/// <summary>
/// Central configuration for E2E test infrastructure.
/// Environment variables can be overridden per-environment (CI, local dev).
///
/// To run E2E tests:
///   1. Ensure 'dotnet' is on PATH (so the API subprocess can be launched).
///   2. Publish the Blazor WASM client:
///        dotnet publish src/FamilySplit.Client -c Release -o /tmp/fs-client
///      Then set:
///        E2E_CLIENT_WWWROOT=/tmp/fs-client/wwwroot
///   3. Run: dotnet test tests/FamilySplit.E2ETests --filter "Category=E2E"
///
/// In CI the E2E job (ci.yml) handles these steps automatically.
/// </summary>
public static class E2EConfig
{
    /// <summary>Port the test API server binds to.</summary>
    public const int ApiPort = 17281;

    /// <summary>Port the in-process static file server (Blazor WASM client) binds to.</summary>
    public const int ClientPort = 17201;

    /// <summary>Base URL of the API — used by Playwright and by the client appsettings override.</summary>
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_API_URL") ?? $"http://localhost:{ApiPort}";

    /// <summary>Base URL of the client SPA — Playwright navigates here.</summary>
    public static string ClientBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_CLIENT_URL") ?? $"http://localhost:{ClientPort}";

    /// <summary>
    /// Path to the published Blazor WASM wwwroot directory.
    /// The client static server serves files from this path.
    /// Required unless E2E_CLIENT_URL points at a separately-running dev server.
    /// </summary>
    public static string? ClientWwwrootPath =>
        Environment.GetEnvironmentVariable("E2E_CLIENT_WWWROOT");

    /// <summary>JWT signing key used by the test API host and by E2ETestBase when minting tokens.</summary>
    public const string TestSigningKey = "e2e-test-signing-key-xxxxxxxxxxxxxxxxxxxxxx";
}
