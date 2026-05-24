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

        // ── Phase 5: Expenses ─────────────────────────────────────────────────
        services.AddScoped<Expenses.ExpenseService>();

        // ── Phase 6: Settlements ──────────────────────────────────────────────
        services.AddScoped<Settlements.SettlementService>();

        return services;
    }
}
