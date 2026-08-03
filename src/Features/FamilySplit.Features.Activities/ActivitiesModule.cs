using FamilySplit.Common.Modules;
using FamilySplit.Features.Activities.AddParticipant;
using FamilySplit.Features.Activities.Close;
using FamilySplit.Features.Activities.Create;
using FamilySplit.Features.Activities.CreateSubActivity;
using FamilySplit.Features.Activities.Data;
using FamilySplit.Features.Activities.GetDetail;
using FamilySplit.Features.Activities.List;
using FamilySplit.Features.Activities.RemoveParticipant;
using FamilySplit.Features.Activities.Update;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Activities;

/// <summary>
/// The Activities feature slice: top-level + depth-1 sub-activities, participant management, and the
/// close flow (parent absorbs open subs). Queries are pure data access; commands hold the business
/// logic and use the strict-CQRS response shapes (201 + id on create / sub-create, 204 on
/// update / close / add-participant / remove-participant). The client re-queries after mutating.
/// </summary>
public sealed class ActivitiesModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(ActivitiesModule).Assembly);

        services.AddScoped<IActivityData, ActivityData>();      // the data seam (ADR-001)
        services.AddScoped<ListActivitiesQueryHandler>();
        services.AddScoped<GetActivityDetailQueryHandler>();
        services.AddScoped<CreateActivityCommandHandler>();
        services.AddScoped<CreateSubActivityCommandHandler>();
        services.AddScoped<UpdateActivityCommandHandler>();
        services.AddScoped<CloseActivityCommandHandler>();
        services.AddScoped<AddParticipantCommandHandler>();
        services.AddScoped<RemoveParticipantCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/groups/{groupId:guid}/activities")
            .WithTags("Activities");

        ListActivitiesEndpoint.Map(grp);
        CreateActivityEndpoint.Map(grp);
        GetActivityDetailEndpoint.Map(grp);
        UpdateActivityEndpoint.Map(grp);
        CloseActivityEndpoint.Map(grp);
        CreateSubActivityEndpoint.Map(grp);
        AddParticipantEndpoint.Map(grp);
        RemoveParticipantEndpoint.Map(grp);
    }
}
