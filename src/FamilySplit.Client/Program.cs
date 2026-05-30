using FamilySplit.Client;
using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using FamilySplit.Client.Store.Admin;
using FamilySplit.Client.Store.Auth;
using FamilySplit.Client.Store.Dashboard;
using FamilySplit.Client.Store.Expenses;
using FamilySplit.Client.Store.Family;
using FamilySplit.Client.Store.FamilyMembers;
using FamilySplit.Client.Store.Groups;
using FamilySplit.Client.Store.Settlements;
using Fluxor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Refit;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- MudBlazor ---------------------------------------------------------------------
builder.Services.AddMudServices();

// --- Fluxor (Redux-style store) ----------------------------------------------------
builder.Services.AddFluxor(o => o
    .ScanAssemblies(typeof(Program).Assembly)
    .UseRouting());

// Explicit registration so Fluxor can resolve effect classes with injected dependencies.
builder.Services.AddScoped<AuthEffects>();
builder.Services.AddScoped<AdminEffects>();
builder.Services.AddScoped<FamilyMemberEffects>();
builder.Services.AddScoped<FamilyEffects>();
builder.Services.AddScoped<GroupEffects>();
builder.Services.AddScoped<ActivityEffects>();
builder.Services.AddScoped<ExpenseEffects>();
builder.Services.AddScoped<SettlementEffects>();
builder.Services.AddScoped<DashboardEffects>();

// --- Auth -------------------------------------------------------------------------
// Singleton (not Scoped) so that the _refreshLock semaphore is shared across every
// handler, component, and effect that touches AuthService. In Blazor WASM a single
// root DI scope means Scoped ≈ Singleton for normal use, but Fluxor effects or
// HttpClientFactory intermediaries can occasionally resolve services from child
// scopes, creating a second instance with its own lock — which lets two concurrent
// POST /auth/refresh calls race past the semaphore and trigger the concurrent-
// detection path on the server.
builder.Services.AddSingleton<AuthService>();
builder.Services.AddTransient<JwtAuthHandler>();
builder.Services.AddTransient<IncludeCredentialsHandler>();

// --- Real-time notifications (SignalR) --------------------------------------------
// Singleton so the connection is shared across all components and survives
// navigation. Connected once after auth, disconnected on logout.
builder.Services.AddSingleton<NotificationHubConnection>();

// --- VAPID push subscription management ------------------------------------------
builder.Services.AddScoped<PushNotificationClientService>();

// --- HTTP / Refit -----------------------------------------------------------------
// Api:BaseUrl is the absolute URL of the API host. Configured per environment:
//   • dev:  "https://localhost:5081"
//   • prod: "https://api.familysplit.net"
// Both client and API share the eTLD+1 (familysplit.net) so cookies set by the
// API are same-site for requests originating from the SPA at app.familysplit.net.
var apiBaseUri = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5081");

// Reuse a single Refit settings instance across all clients — avoids re-creating
// the JsonSerializerOptions for every registration on cold start.
var refitSettings = new RefitSettings();

// Public API — no auth header, no credentials. Short explicit timeout since
// a health check that doesn't respond in 10 s is itself a failure signal.
builder.Services.AddRefitClient<IHealthApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = apiBaseUri;
        c.Timeout = TimeSpan.FromSeconds(10);
    });

// Authenticated APIs — attach Bearer token from AuthService via JwtAuthHandler.
AddAuthedClient<IWhoAmIApi>();
AddAuthedClient<IFamilyMemberClient>();
AddAuthedClient<IFamilyClient>();
AddAuthedClient<IAdminClient>();
AddAuthedClient<IGroupClient>();
AddAuthedClient<IActivityClient>();
AddAuthedClient<IExpenseClient>();
AddAuthedClient<ISettlementClient>();
AddAuthedClient<IDashboardClient>();
AddAuthedClient<IPushClient>();

// Auth API (refresh + logout) — needs credentials=include so the HttpOnly
// refresh cookie is attached on every call.
// No resilience handler here: POST /auth/refresh rotates the token row and is
// NOT idempotent — retrying a failed rotation could trigger theft-detection.
builder.Services.AddRefitClient<IAuthApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = apiBaseUri)
    .AddHttpMessageHandler<IncludeCredentialsHandler>();

await builder.Build().RunAsync();

void AddAuthedClient<TClient>() where TClient : class
{
    builder.Services.AddRefitClient<TClient>(refitSettings)
        .ConfigureHttpClient(c =>
        {
            c.BaseAddress = apiBaseUri;
            // Disable the default 100-s HttpClient timeout — the resilience
            // pipeline's TotalRequestTimeout acts as the authoritative deadline.
            c.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        })
        // JwtAuthHandler is outermost: it attaches the Bearer token and handles
        // 401 by refreshing once and retrying. It wraps the resilience pipeline
        // so transient 5xx failures are retried before JwtAuthHandler ever sees
        // them — no unnecessary token refreshes for server-side blips.
        .AddHttpMessageHandler<JwtAuthHandler>()
        // Standard resilience pipeline (Polly v8 under the hood):
        //   • TotalRequestTimeout  — overall deadline including all retries
        //   • Retry                — exponential back-off with jitter on 5xx /
        //                           network errors / 408 / 429; never on 4xx
        //   • CircuitBreaker       — stops hammering an unavailable server
        //   • AttemptTimeout       — per-attempt deadline
        .AddStandardResilienceHandler(o =>
        {
            o.Retry.MaxRetryAttempts = 2;       // 3 total attempts
            o.Retry.UseJitter = true;    // spread retries across clients
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
        });
}
