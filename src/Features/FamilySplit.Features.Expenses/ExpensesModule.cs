using FamilySplit.Common.Modules;
using FamilySplit.Features.Expenses.Create;
using FamilySplit.Features.Expenses.Delete;
using FamilySplit.Features.Expenses.GetDetail;
using FamilySplit.Features.Expenses.List;
using FamilySplit.Features.Expenses.Update;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilySplit.Features.Expenses;

/// <summary>
/// The Expenses feature slice: expense CRUD scoped to an activity. Queries are pure
/// data access; commands hold the business logic and use the strict-CQRS response
/// shapes (201 + id on create, 204 elsewhere).
/// </summary>
public sealed class ExpensesModule : IFeatureModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(ExpensesModule).Assembly);

        services.AddScoped<ListExpensesQueryHandler>();
        services.AddScoped<GetExpenseDetailQueryHandler>();
        services.AddScoped<CreateExpenseCommandHandler>();
        services.AddScoped<UpdateExpenseCommandHandler>();
        services.AddScoped<DeleteExpenseCommandHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var grp = endpoints
            .MapGroup("/groups/{groupId:guid}/activities/{activityId:guid}/expenses")
            .WithTags("Expenses");

        ListExpensesEndpoint.Map(grp);
        CreateExpenseEndpoint.Map(grp);
        GetExpenseDetailEndpoint.Map(grp);
        UpdateExpenseEndpoint.Map(grp);
        DeleteExpenseEndpoint.Map(grp);
    }
}
