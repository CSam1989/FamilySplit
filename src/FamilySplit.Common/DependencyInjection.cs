using FamilySplit.Common.Auditing;
using FamilySplit.Common.Security;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Common;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the cross-slice services. Both are scoped so they share the
    /// request's pooled <c>AppDbContext</c> unit-of-work: AuditService.Queue()
    /// adds rows to the change tracker and the calling handler's
    /// SaveChangesAsync() persists them atomically.
    /// </summary>
    public static IServiceCollection AddFamilySplitCommon(this IServiceCollection services)
    {
        services.AddScoped<AuditService>();
        services.AddScoped<GroupMembershipGuard>();
        return services;
    }
}
