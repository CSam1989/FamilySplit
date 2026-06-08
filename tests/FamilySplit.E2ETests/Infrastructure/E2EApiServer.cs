using System.Diagnostics;
using System.Net;
using System.Net.Http;
using FamilySplit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FamilySplit.E2ETests.Infrastructure;

/// <summary>
/// Manages the full E2E server stack for one test collection:
///   1. Starts a PostgreSQL container (postgres:16-alpine) via Testcontainers.
///   2. Applies EF Core migrations to the container.
///   3. Starts the FamilySplit.Api process as a subprocess pointed at the container DB.
///
/// Shared via xUnit's <see cref="ICollectionFixture{T}"/> so the container and
/// process are created once per collection and torn down when all tests finish.
///
/// Thread-safety: xUnit serialises tests within a collection, so concurrent access
/// is not a concern.
/// </summary>
public sealed class E2EApiServer : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("familysplit_e2e")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private Process? _apiProcess;

    // Exposed to tests for direct DB seeding (not transactional — each test is responsible
    // for cleaning up its own seed data or relying on unique data per test).
    public string DbConnectionString { get; private set; } = "";

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async ValueTask InitializeAsync()
    {
        // 1. Start Postgres container.
        await _dbContainer.StartAsync();
        DbConnectionString = _dbContainer.GetConnectionString();

        // 2. Apply EF Core migrations using a real DbContext instance.
        await ApplyMigrationsAsync();

        // 3. Start the API subprocess.
        await StartApiProcessAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Tear down in reverse order.
        KillApiProcess();

        await _dbContainer.DisposeAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ApplyMigrationsAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DbConnectionString, npg =>
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));

        await using var context = new AppDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();
    }

    /// <summary>
    /// Locates the FamilySplit.Api project binary and starts it as a subprocess
    /// with environment variables wired to the Testcontainers DB.
    ///
    /// Uses 'dotnet run --no-build' so the project must have been built first.
    /// In CI this is guaranteed by the 'build' job. Locally, run 'dotnet build'
    /// before executing the E2E suite.
    /// </summary>
    private async Task StartApiProcessAsync()
    {
        var apiProjectPath = FindApiProjectPath();

        var env = new Dictionary<string, string>
        {
            // Override the connection string so the API uses the test container.
            ["ConnectionStrings__Postgres"] = DbConnectionString,

            // Use a fixed signing key that E2ETestBase uses when minting tokens.
            ["Jwt__SigningKey"] = E2EConfig.TestSigningKey,
            ["Jwt__Issuer"] = "familysplit",
            ["Jwt__Audience"] = "familysplit-client",

            // Bind to our fixed E2E port.
            ["ASPNETCORE_URLS"] = E2EConfig.ApiBaseUrl,

            // Allow the client origin so refresh/CORS works.
            ["Cors__AllowedOrigins__0"] = E2EConfig.ClientBaseUrl,

            // Use Development environment so appsettings.Development.json settings apply.
            ["ASPNETCORE_ENVIRONMENT"] = "Development",

            // Data Protection keys need somewhere to live — use an in-memory store
            // by disabling the key persistence requirement in test mode.
            // The API uses ApplicationName-based isolation anyway.
            ["DataProtection__DisableAutomaticKeyGeneration"] = "false",
        };

        var psi = new ProcessStartInfo("dotnet")
        {
            // -c Release must match the configuration used to build the binary.
            // Without it, dotnet run --no-build looks in bin/Debug/ and exits
            // immediately (connection refused), causing a silent 60-second timeout.
            Arguments = $"run --no-build -c Release --no-launch-profile --project \"{apiProjectPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = apiProjectPath,
        };

        foreach (var (key, value) in env)
            psi.EnvironmentVariables[key] = value;

        _apiProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start API process.");

        // Capture stdout/stderr so a startup crash is visible in test output.
        var outputLines = new System.Collections.Concurrent.ConcurrentBag<string>();
        _apiProcess.OutputDataReceived += (_, e) => { if (e.Data != null) outputLines.Add(e.Data); };
        _apiProcess.ErrorDataReceived += (_, e) => { if (e.Data != null) outputLines.Add($"ERR: {e.Data}"); };
        _apiProcess.BeginOutputReadLine();
        _apiProcess.BeginErrorReadLine();

        // Wait until the health endpoint responds (up to 60 s).
        await WaitForHealthyAsync(TimeSpan.FromSeconds(60), _apiProcess, outputLines);
    }

    private static async Task WaitForHealthyAsync(
        TimeSpan timeout,
        Process process,
        System.Collections.Concurrent.ConcurrentBag<string> outputLines)
    {
        using var http = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            // Fail fast: if the process already exited it will never serve /health.
            if (process.HasExited)
            {
                var output = string.Join(Environment.NewLine, outputLines);
                throw new InvalidOperationException(
                    $"API process exited with code {process.ExitCode} before becoming healthy.{Environment.NewLine}{output}");
            }

            try
            {
                var response = await http.GetAsync($"{E2EConfig.ApiBaseUrl}/health");
                if (response.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch
            {
                // Not yet ready — wait and retry.
            }

            await Task.Delay(500);
        }

        var finalOutput = string.Join(Environment.NewLine, outputLines);
        throw new TimeoutException(
            $"API at {E2EConfig.ApiBaseUrl}/health did not become healthy within {timeout.TotalSeconds}s.{Environment.NewLine}{finalOutput}");
    }

    private void KillApiProcess()
    {
        if (_apiProcess is null) return;
        try
        {
            if (!_apiProcess.HasExited)
                _apiProcess.Kill(entireProcessTree: true);

            _apiProcess.Dispose();
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    /// <summary>
    /// Finds the FamilySplit.Api project directory by walking up from the test
    /// binary output directory until the solution root (containing FamilySplit.slnx)
    /// is found, then resolving src/FamilySplit.Api from there.
    /// </summary>
    private static string FindApiProjectPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FamilySplit.slnx")))
            {
                var apiPath = Path.Combine(dir.FullName, "src", "FamilySplit.Api");
                if (Directory.Exists(apiPath))
                    return apiPath;

                throw new DirectoryNotFoundException(
                    $"Found solution root at {dir.FullName} but src/FamilySplit.Api is missing.");
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate solution root (directory containing FamilySplit.slnx) " +
            $"by walking up from {AppContext.BaseDirectory}.");
    }
}
