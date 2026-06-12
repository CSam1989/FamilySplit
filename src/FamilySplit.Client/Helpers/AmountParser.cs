using System.Globalization;

namespace FamilySplit.Client.Helpers;

/// <summary>
/// Tolerant parser for user-typed money amounts. Accepts both "." and "," as the
/// decimal separator and rejects thousands grouping outright, so a comma typed on
/// an EU iOS keypad can never be misread as a grouping separator ("12,50" → 1250).
/// </summary>
public static class AmountParser
{
    public static bool TryParse(string? input, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var s = input.Trim()
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(',', '.');

        // More than one separator means grouping (e.g. "1.234.56") — ambiguous, reject.
        if (s.Count(c => c == '.') > 1) return false;

        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}
