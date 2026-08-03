using FamilySplit.Features.Expenses.Create;

namespace FamilySplit.UnitTests.Features.Expenses.Create;

public class CreateExpenseCommandValidatorTests
{
    private static readonly DateOnly ValidDate = new(2024, 1, 1);

    private static CreateExpenseCommand ValidCreate(
        string title = "Groceries",
        string? description = null,
        decimal totalAmount = 10.00m,
        string? currency = "USD",
        DateOnly expenseDate = default,
        Guid? categoryId = null)
        => new(title, description, totalAmount, currency,
               expenseDate == default ? ValidDate : expenseDate, categoryId);

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTitle_FailsWithMessage(string title)
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(title: title));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void TitleTooLong_FailsWithMessage()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(title: new string('A', 201)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title must be at most 200 characters.");
    }

    [Fact]
    public void TitleExactly200Chars_PassesValidation()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(title: new string('A', 200)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NullDescription_PassesValidation()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(description: null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DescriptionTooLong_FailsWithMessage()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(description: new string('B', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage == "Description must be at most 500 characters.");
    }

    [Fact]
    public void DescriptionExactly500Chars_PassesValidation()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(description: new string('B', 500)));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TotalAmountNotPositive_FailsWithMessage(decimal amount)
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(totalAmount: amount));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TotalAmount" && e.ErrorMessage == "Amount must be greater than 0.");
    }

    [Fact]
    public void NullCurrency_PassesValidation()
    {
        var validator = new CreateExpenseCommandValidator();
        var result = validator.Validate(ValidCreate(currency: null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void CurrencyNotThreeChars_FailsWithMessage(string currency)
    {
        var validator = new CreateExpenseCommandValidator();
        // Empty string is still non-null, so the When condition fires
        var result = validator.Validate(ValidCreate(currency: currency));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency" && e.ErrorMessage == "Currency must be a 3-letter ISO code.");
    }

    [Fact]
    public void DefaultExpenseDate_FailsWithMessage()
    {
        var validator = new CreateExpenseCommandValidator();
        var cmd = new CreateExpenseCommand("Groceries", null, 10m, "USD", default, null);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpenseDate" && e.ErrorMessage == "Expense date is required.");
    }
}
