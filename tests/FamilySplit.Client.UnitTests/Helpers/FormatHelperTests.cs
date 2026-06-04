using FamilySplit.Client.Helpers;

namespace FamilySplit.Client.UnitTests.Helpers;

public class FormatHelperTests
{
    // -------------------------------------------------------------------------
    // FormatAmount
    // -------------------------------------------------------------------------

    [Fact]
    public void FormatAmount_Eur_PrefixesEuroSign()
    {
        var result = FormatHelper.FormatAmount(12.50m, "EUR");
        result.Should().StartWith("€");
    }

    [Fact]
    public void FormatAmount_Usd_PrefixesDollarSign()
    {
        var result = FormatHelper.FormatAmount(12.50m, "USD");
        result.Should().StartWith("$");
    }

    [Fact]
    public void FormatAmount_Gbp_PrefixesPoundSign()
    {
        var result = FormatHelper.FormatAmount(12.50m, "GBP");
        result.Should().StartWith("£");
    }

    [Fact]
    public void FormatAmount_UnknownCurrency_PrefixesCurrencyCodeWithSpace()
    {
        var result = FormatHelper.FormatAmount(12.50m, "CHF");
        result.Should().StartWith("CHF ");
    }

    [Fact]
    public void FormatAmount_AlwaysFormatsTwoDecimalPlaces()
    {
        // Whole number — must still show ".00"
        var result = FormatHelper.FormatAmount(100m, "EUR");
        result.Should().EndWith(".00");
    }

    [Fact]
    public void FormatAmount_SingleDecimalDigit_PadsToTwo()
    {
        // 12.5 → "12.50"
        var result = FormatHelper.FormatAmount(12.5m, "EUR");
        result.Should().EndWith(".50");
    }

    [Fact]
    public void FormatAmount_Zero_ReturnsSymbolFollowedByZero()
    {
        var result = FormatHelper.FormatAmount(0m, "EUR");
        result.Should().StartWith("€");
        result.Should().EndWith(".00");
    }

    [Fact]
    public void FormatAmount_Negative_ContainsMinusSign()
    {
        var result = FormatHelper.FormatAmount(-5.75m, "EUR");
        result.Should().Contain("-");
        result.Should().EndWith(".75");
    }

    [Fact]
    public void FormatAmount_LargeAmount_ContainsExpectedDigits()
    {
        // 1 234 567.89 — exact group separator depends on culture, so we only
        // verify the fractional part and that the integer digits are all present.
        var result = FormatHelper.FormatAmount(1234567.89m, "USD");
        result.Should().StartWith("$");
        result.Should().EndWith(".89");
        result.Should().Contain("1");
        result.Should().Contain("7");
    }

    [Fact]
    public void FormatAmount_SmallFraction_FormatsCorrectly()
    {
        var result = FormatHelper.FormatAmount(0.01m, "GBP");
        result.Should().StartWith("£");
        result.Should().EndWith(".01");
    }

    // -------------------------------------------------------------------------
    // AvatarColor
    // -------------------------------------------------------------------------

    [Fact]
    public void AvatarColor_SameInput_ReturnsSameColorOnEveryCall()
    {
        var first = FormatHelper.AvatarColor("Alice");
        var second = FormatHelper.AvatarColor("Alice");
        var third = FormatHelper.AvatarColor("Alice");

        first.Should().Be(second);
        second.Should().Be(third);
    }

    [Fact]
    public void AvatarColor_ReturnsHexColor()
    {
        // All palette entries are six-digit hex strings starting with '#'
        var result = FormatHelper.AvatarColor("Bob");
        result.Should().MatchRegex(@"^#[0-9A-Fa-f]{6}$");
    }

    [Fact]
    public void AvatarColor_DifferentNames_CanProduceDifferentColors()
    {
        // The palette has 8 entries; with more than 8 distinct names at least
        // some must differ. We assert that not every name in our set maps to the
        // same color — if they all matched, the palette selection would be broken.
        var names = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Hank", "Ivan" };
        var colors = names.Select(FormatHelper.AvatarColor).Distinct().ToList();

        colors.Should().HaveCountGreaterThan(1,
            "a palette of 8 colors should produce more than one distinct result across 9 names");
    }

    [Fact]
    public void AvatarColor_EmptyString_DoesNotThrow()
    {
        // Empty string has a well-defined GetHashCode(); the method must not throw.
        var act = () => FormatHelper.AvatarColor(string.Empty);
        act.Should().NotThrow();
    }

    [Fact]
    public void AvatarColor_EmptyString_ReturnsHexColor()
    {
        var result = FormatHelper.AvatarColor(string.Empty);
        result.Should().MatchRegex(@"^#[0-9A-Fa-f]{6}$");
    }

    [Fact]
    public void AvatarColor_LongName_DoesNotThrow()
    {
        var longName = new string('A', 500);
        var act = () => FormatHelper.AvatarColor(longName);
        act.Should().NotThrow();
    }

    [Fact]
    public void AvatarColor_ResultIsOneOfThePaletteEntries()
    {
        var palette = new[]
        {
            "#4F46E5", "#7C3AED", "#DB2777", "#DC2626",
            "#D97706", "#059669", "#0891B2", "#0284C7",
        };

        var result = FormatHelper.AvatarColor("SomeName");

        palette.Should().Contain(result,
            "the result must be one of the eight fixed palette colors");
    }
}
