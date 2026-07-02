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
        // Migrated to FamilySplit.Features.Families (vertical slice) — registered
        // via FamiliesModule in the API host.

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

        // ── Auth ──────────────────────────────────────────────────────────────
        // Migrated to FamilySplit.Features.Auth (vertical slice) — registered
        // via AuthModule in the API host.

        // ── VAPID push / notifications ──────────────────────────────────────────
        // Migrated to FamilySplit.Features.Notifications (vertical slice) — registered
        // via NotificationsModule in the API host.

        return services;
    }
}
