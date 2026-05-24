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
using Fluxor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
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

// --- Auth (sessionStorage-backed) -------------------------------------------------
builder.Services.AddScoped<AuthService>();
builder.Services.AddTransient<JwtAuthHandler>();
builder.Services.AddTransient<IncludeCredentialsHandler>();

// --- HTTP / Refit -----------------------------------------------------------------
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
                 ?? "https://localhost:5081";

// Public API — no auth header, no credentials.
builder.Services
    .AddRefitClient<IHealthApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl));

// Authenticated API — attaches Bearer token from AuthService.
builder.Services
    .AddRefitClient<IWhoAmIApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IFamilyMemberClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IFamilyClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IAdminClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IGroupClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IActivityClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<IExpenseClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

builder.Services
    .AddRefitClient<ISettlementClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<JwtAuthHandler>();

// Handoff API — needs credentials=include to send the HttpOnly handoff cookie.
builder.Services
    .AddRefitClient<IHandoffApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<IncludeCredentialsHandler>();

await builder.Build().RunAsync();
