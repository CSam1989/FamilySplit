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

        // ── Future phases ─────────────────────────────────────────────────────
        //   services.AddScoped<Activities.ActivityService>();
        //   services.AddScoped<Expenses.ExpenseService>();
        //   services.AddScoped<Settlements.SettlementService>();

        return services;
    }
}
