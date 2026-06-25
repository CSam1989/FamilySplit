using FamilySplit.Common.Modules;
using FamilySplit.Features.Settlements.ConfirmReceived;
using FamilySplit.Features.Settlements.ConfirmSent;
using FamilySplit.Features.Settlements.Data;
using FamilySplit.Features.Settlements.Generate;
using FamilySplit.Features.Settlements.GetBalances;
using FamilySplit.Features.Settlements.GetDetail;
using FamilySplit.Features.Settlements.List;
using FamilySplit.Features.Settlements.ListForGroup;
using FamilySplit.Features.Settlements.ListMyPending;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Settlements;

/// <summary>
/// The Settlements feature slice: per-family balance view, optimised settlement generation, and the
/// ConfirmSent / ConfirmReceived approval flow (the activity transitions to Settled when all its
/// settlements complete). Queries are pure data access; commands hold the business logic and use the
/// strict-CQRS response shapes (204 on generate / confirm-sent / confirm-received — the client
/// re-queries the list). Commands take only route ids, so the slice has no validators.
/// </summary>
public sealed class SettlementsModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISettlementData, SettlementData>();      // the data seam (ADR-001)

        services.AddScoped<GetBalancesQueryHandler>();
        services.AddScoped<ListSettlementsQueryHandler>();
        services.AddScoped<GetSettlementDetailQueryHandler>();
        services.AddScoped<ListForGroupQueryHandler>();
        services.AddScoped<ListMyPendingQueryHandler>();

        services.AddScoped<GenerateSettlementsCommandHandler>();
        services.AddScoped<ConfirmSentCommandHandler>();
        services.AddScoped<ConfirmReceivedCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/settlements")
            .WithTags("Settlements");

        ListSettlementsEndpoint.Map(grp);
        GenerateSettlementsEndpoint.Map(grp);
        GetSettlementDetailEndpoint.Map(grp);
        ConfirmSentEndpoint.Map(grp);
        ConfirmReceivedEndpoint.Map(grp);

        // Standalone routes (different prefixes) — each maps itself with WithTags("Settlements").
        ListMyPendingEndpoint.Map(endpoints);
        ListForGroupEndpoint.Map(endpoints);
        GetBalancesEndpoint.Map(endpoints);
    }
}
