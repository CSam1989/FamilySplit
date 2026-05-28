using FluentValidation;
using FamilySplit.Application.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFamilySplitApplication(this IServiceCollection services)
    {
        // Registers all FluentValidation validators in this assembly automatically.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // ── Audit logging ─────────────────────────────────────────────────────
        // Scoped so it shares the same AppDbContext as the calling service.
        // AuditService.Queue() adds rows to the change tracker; the caller's
        // SaveChangesAsync() persists them atomically with the main mutation.
        services.AddScoped<AuditService>();

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

        // ── Dashboard stats ───────────────────────────────────────────────────
        services.AddScoped<Dashboard.DashboardService>();

        // ── Auth: refresh token rotation / revocation ─────────────────────────
        services.AddScoped<Auth.RefreshTokenService>();

        return services;
    }
}
