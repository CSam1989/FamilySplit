using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFamilySplitApplication(this IServiceCollection services)
    {
        // Registers all FluentValidation validators in this assembly automatically.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // ── Global-admin operations ────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Admin (vertical slice) — registered
        // via AdminModule in the API host.

        // ── Own-family management (family admin) ──────────────────────────────
        services.AddScoped<Families.FamilyService>();

        // ── Group operations ──────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Groups (vertical slice) — registered
        // via GroupsModule in the API host.

        // ── Activities ────────────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Activities (vertical slice) — registered
        // via ActivitiesModule in the API host.

        // ── Expenses ──────────────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Expenses (vertical slice) — registered
        // via ExpensesModule in the API host.

        // ── Settlements ───────────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Settlements (vertical slice) — registered
        // via SettlementsModule in the API host.

        // ── Dashboard stats ───────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Dashboard (vertical slice) — registered
        // via DashboardModule in the API host.

        // ── Auth: refresh token rotation / revocation ─────────────────────────
        services.AddScoped<Auth.RefreshTokenService>();

        // ── VAPID push notifications ──────────────────────────────────────────
        // Scoped so it shares the AppDbContext with its callers.
        // INotificationService is registered in the API layer (needs IHubContext).
        services.AddScoped<Push.PushNotificationService>();

        return services;
    }
}
