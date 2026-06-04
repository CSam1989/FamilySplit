namespace FamilySplit.Application.Expenses;

/// <summary>Pure guard: determines whether an expense update requires re-snapshotting weights and recalculating shares.</summary>
public static class ExpenseReshuffleRequired
{
    public static bool Check(decimal oldAmount, decimal newAmount, DateOnly oldDate, DateOnly newDate)
        => oldAmount != newAmount || oldDate != newDate;
}
