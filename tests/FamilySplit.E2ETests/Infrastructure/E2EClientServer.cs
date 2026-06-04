using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace FamilySplit.E2ETests.Infrastructure;

/// <summary>
/// In-process static file server that serves the published Blazor WASM client.
///
/// The server intercepts two special paths:
///   • GET /appsettings.json — returns a JSON blob that overrides Api:BaseUrl to point at
///     the E2E test API server.  This allows the same published output to be reused across
///     environments without re-publishing.
///   • All other paths — served from <see cref="E2EConfig.ClientWwwrootPath"/> as static files.
///   • SPA fallback — any path that doesn't match a file returns index.html so the Blazor
///     router can handle client-side navigation.
///
/// Pre-requisites:
///   1. The Blazor client must have been published:
///        dotnet publish src/FamilySplit.Client -c Release -o /tmp/fs-client
///   2. Set E2E_CLIENT_WWWROOT=/tmp/fs-client/wwwroot
///      OR pass the path when constructing this class.
///
/// If no wwwroot path is available, <see cref="IsAvailable"/> returns false and the
/// E2ETestBase will skip browser-dependent tests rather than fail.
/// </summary>
public sealed class E2EClientServer : IAsyncLifetime
{
    private WebApplication? _app;

    /// <summary>True when a wwwroot path is configured and the server started successfully.</summary>
    public bool IsAvailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var wwwrootPath = E2EConfig.ClientWwwrootPath;

        if (string.IsNullOrWhiteSpace(wwwrootPath) || !Directory.Exists(wwwrootPath))
        {
            // Skip serving the client — browser-dependent tests will be skipped.
            IsAvailable = false;
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // Run on a separate port from the API.
            ApplicationName = "FamilySplit.E2EClientServer",
        });

        builder.WebHost.UseUrls($"http://localhost:{E2EConfig.ClientPort}");

        _app = builder.Build();

        // ── Intercept appsettings.json ────────────────────────────────────────
        // The published Blazor WASM app fetches /appsettings.json at startup to
        // read Api:BaseUrl.  We return a custom response that points to the test
        // API server so the client talks to our Testcontainers-backed API.
        _app.MapGet("/appsettings.json", () => Results.Json(new
        {
            Api = new { BaseUrl = E2EConfig.ApiBaseUrl },
            Logging = new { LogLevel = new { Default = "Warning" } },
        }));

        // ── Serve static files from the published wwwroot ─────────────────────
        // ServeUnknownFileTypes covers Blazor's .wasm, .dat, and other non-standard types.
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".wasm"] = "application/wasm";
        contentTypeProvider.Mappings[".dat"] = "application/octet-stream";

        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwrootPath),
            ContentTypeProvider = contentTypeProvider,
            ServeUnknownFileTypes = true,
        });

        // ── SPA fallback ──────────────────────────────────────────────────────
        // Any URL that doesn't match a static file gets index.html so that the
        // Blazor router can handle the navigation.
        _app.MapFallback(async ctx =>
        {
            var indexPath = Path.Combine(wwwrootPath, "index.html");
            ctx.Response.ContentType = "text/html";
            await ctx.Response.SendFileAsync(indexPath);
        });

        await _app.StartAsync();

        // Verify the client server is reachable.
        using var http = new HttpClient();
        try
        {
            var res = await http.GetAsync($"{E2EConfig.ClientBaseUrl}/");
            IsAvailable = res.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotModified;
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
