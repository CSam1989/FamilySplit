using Refit;

namespace FamilySplit.Client.Services;

[Headers("Content-Type: application/json")]
public interface IExpenseClient
{
    [Get("/groups/{groupId}/activities/{activityId}/expenses")]
    Task<List<ExpenseSummaryDto>> ListAsync(Guid groupId, Guid activityId);

    [Get("/groups/{groupId}/activities/{activityId}/expenses/{expenseId}")]
    Task<ExpenseDetailDto> GetAsync(Guid groupId, Guid activityId, Guid expenseId);

    [Post("/groups/{groupId}/activities/{activityId}/expenses")]
    Task<ExpenseDetailDto> CreateAsync(Guid groupId, Guid activityId, [Body] CreateExpenseRequest request);

    [Put("/groups/{groupId}/activities/{activityId}/expenses/{expenseId}")]
    Task<ExpenseDetailDto> UpdateAsync(Guid groupId, Guid activityId, Guid expenseId, [Body] UpdateExpenseRequest request);

    [Delete("/groups/{groupId}/activities/{activityId}/expenses/{expenseId}")]
    Task DeleteAsync(Guid groupId, Guid activityId, Guid expenseId);
}
