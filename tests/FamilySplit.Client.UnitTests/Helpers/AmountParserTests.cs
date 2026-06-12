using FamilySplit.Client.Helpers;

namespace FamilySplit.Client.UnitTests.Helpers;

public sealed class AmountParserTests
{
    [Theory]
    [InlineData("12.50", 12.50)]
    [InlineData("12,50", 12.50)]
    [InlineData("4,5", 4.5)]
    [InlineData("0.01", 0.01)]
    [InlineData("250", 250)]
    [InlineData(" 19,99 ", 19.99)]
    [InlineData("1000000", 1_000_000)]
    public void TryParse_ValidInput_ReturnsExpectedValue(string input, decimal expected)
    {
        var ok = AmountParser.TryParse(input, out var value);

        ok.Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("1.234.56")]   // grouping — ambiguous, must be rejected, never read as 1234.56
    [InlineData("1,234,56")]
    [InlineData("1.234,56")]
    [InlineData("abc")]
    [InlineData("12.5x")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("-5")]         // sign not allowed for money input
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        var ok = AmountParser.TryParse(input, out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryParse_CommaNeverTreatedAsGrouping()
    {
        // The exact iOS-EU corruption case: "4,50" must be 4.50, never 450.
        AmountParser.TryParse("4,50", out var value).Should().BeTrue();
        value.Should().Be(4.50m);
        value.Should().NotBe(450m);
    }
}
