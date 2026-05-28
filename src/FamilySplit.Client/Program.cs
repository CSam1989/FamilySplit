using FamilySplit.Client;
using FamilySplit.Client.Services;
using FamilySplit.Client.Store.Activities;
using FamilySplit.Client.Store.Expenses;
using FamilySplit.Client.Store.Settlements;
using FamilySplit.Client.Store.Admin;
using FamilySplit.Client.Store.Auth;
using FamilySplit.Client.Store.Family;
using FamilySplit.Client.Store.FamilyMembers;
using FamilySplit.Client.Store.Groups;
using FamilySplit.Client.Store.Dashboard;
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
var apiBaseUri = new Uri(builder.Configuration["Api:BaseUrl"] ?? "https://localhost:5081");

// Reuse a single Refit settings instance across all clients — avoids re-creating
// the JsonSerializerOptions for every registration on cold start.
var refitSettings = new RefitSettings();

// Public API — no auth header, no credentials.
builder.Services.AddRefitClient<IHealthApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = apiBaseUri);

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
builder.Services.AddRefitClient<IAuthApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = apiBaseUri)
    .AddHttpMessageHandler<IncludeCredentialsHandler>();

await builder.Build().RunAsync();

void AddAuthedClient<TClient>() where TClient : class
{
    builder.Services.AddRefitClient<TClient>(refitSettings)
        .ConfigureHttpClient(c => c.BaseAddress = apiBaseUri)
        .AddHttpMessageHandler<JwtAuthHandler>();
}
