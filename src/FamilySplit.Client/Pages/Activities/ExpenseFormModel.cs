namespace FamilySplit.Client.Pages.Activities;

public class ExpenseFormModel
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? TotalAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
}
