using FamilySplit.Application.Expenses;
using FamilySplit.Application.Expenses.Dtos;
using FluentAssertions;

namespace FamilySplit.UnitTests.Expenses;

public class ExpenseValidatorTests
{
    private static readonly DateOnly ValidDate = new(2024, 1, 1);

    // ── CreateExpenseValidator ──────────────────────────────────────────────

    private static CreateExpenseRequest ValidCreate(
        string title = "Groceries",
        string? description = null,
        decimal totalAmount = 10.00m,
        string? currency = "USD",
        DateOnly expenseDate = default,
        Guid? categoryId = null)
        => new(title, description, totalAmount, currency,
               expenseDate == default ? ValidDate : expenseDate, categoryId);

    [Fact]
    public void CreateExpenseValidator_ValidRequest_PassesValidation()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateExpenseValidator_EmptyTitle_FailsWithMessage(string title)
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(title: title));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void CreateExpenseValidator_TitleTooLong_FailsWithMessage()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(title: new string('A', 201)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title must be at most 200 characters.");
    }

    [Fact]
    public void CreateExpenseValidator_TitleExactly200Chars_PassesValidation()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(title: new string('A', 200)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateExpenseValidator_NullDescription_PassesValidation()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(description: null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateExpenseValidator_DescriptionTooLong_FailsWithMessage()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(description: new string('B', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage == "Description must be at most 500 characters.");
    }

    [Fact]
    public void CreateExpenseValidator_DescriptionExactly500Chars_PassesValidation()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(description: new string('B', 500)));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateExpenseValidator_TotalAmountNotPositive_FailsWithMessage(decimal amount)
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(totalAmount: amount));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TotalAmount" && e.ErrorMessage == "Amount must be greater than 0.");
    }

    [Fact]
    public void CreateExpenseValidator_NullCurrency_PassesValidation()
    {
        var validator = new CreateExpenseValidator();
        var result = validator.Validate(ValidCreate(currency: null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void CreateExpenseValidator_CurrencyNotThreeChars_FailsWithMessage(string currency)
    {
        var validator = new CreateExpenseValidator();
        // Empty string is still non-null, so the When condition fires
        var result = validator.Validate(ValidCreate(currency: currency));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency" && e.ErrorMessage == "Currency must be a 3-letter ISO code.");
    }

    [Fact]
    public void CreateExpenseValidator_DefaultExpenseDate_FailsWithMessage()
    {
        var validator = new CreateExpenseValidator();
        var req = new CreateExpenseRequest("Groceries", null, 10m, "USD", default, null);
        var result = validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpenseDate" && e.ErrorMessage == "Expense date is required.");
    }

    // ── UpdateExpenseValidator ──────────────────────────────────────────────

    private static UpdateExpenseRequest ValidUpdate(
        string title = "Groceries",
        string? description = null,
        decimal totalAmount = 10.00m,
        string? currency = "USD",
        DateOnly expenseDate = default,
        Guid? categoryId = null)
        => new(title, description, totalAmount, currency,
               expenseDate == default ? ValidDate : expenseDate, categoryId);

    [Fact]
    public void UpdateExpenseValidator_ValidRequest_PassesValidation()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateExpenseValidator_EmptyTitle_FailsWithMessage(string title)
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(title: title));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void UpdateExpenseValidator_TitleTooLong_FailsWithMessage()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(title: new string('A', 201)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title" && e.ErrorMessage == "Title must be at most 200 characters.");
    }

    [Fact]
    public void UpdateExpenseValidator_TitleExactly200Chars_PassesValidation()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(title: new string('A', 200)));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateExpenseValidator_NullDescription_PassesValidation()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(description: null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateExpenseValidator_DescriptionTooLong_FailsWithMessage()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(description: new string('B', 501)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage == "Description must be at most 500 characters.");
    }

    [Fact]
    public void UpdateExpenseValidator_DescriptionExactly500Chars_PassesValidation()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(description: new string('B', 500)));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateExpenseValidator_TotalAmountNotPositive_FailsWithMessage(decimal amount)
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(totalAmount: amount));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TotalAmount" && e.ErrorMessage == "Amount must be greater than 0.");
    }

    [Fact]
    public void UpdateExpenseValidator_NullCurrency_PassesValidation()
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(currency: null));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void UpdateExpenseValidator_CurrencyNotThreeChars_FailsWithMessage(string currency)
    {
        var validator = new UpdateExpenseValidator();
        var result = validator.Validate(ValidUpdate(currency: currency));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Currency" && e.ErrorMessage == "Currency must be a 3-letter ISO code.");
    }

    [Fact]
    public void UpdateExpenseValidator_DefaultExpenseDate_FailsWithMessage()
    {
        var validator = new UpdateExpenseValidator();
        var req = new UpdateExpenseRequest("Groceries", null, 10m, "USD", default, null);
        var result = validator.Validate(req);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ExpenseDate" && e.ErrorMessage == "Expense date is required.");
    }
}
