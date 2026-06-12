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
        services.AddScoped<Admin.AdminService>();

        // ── Own-family management (family admin) ──────────────────────────────
        services.AddScoped<Families.FamilyService>();

        // ── Group operations ──────────────────────────────────────────────────
        services.AddScoped<Groups.GroupService>();

        // ── Phase 4: Activities ───────────────────────────────────────────────
        services.AddScoped<Core.ParticipantSeeder>();
        services.AddScoped<Activities.ActivityService>();

        // ── Expenses ──────────────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Expenses (vertical slice) — registered
        // via ExpensesModule in the API host.

        // ── Phase 6: Settlements ──────────────────────────────────────────────
        services.AddScoped<Settlements.SettlementService>();

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
