using System.Globalization;

namespace FamilySplit.Client.Helpers;

/// <summary>
/// Pure static helpers used across Blazor pages and shared components.
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// Formats a monetary amount with the appropriate currency symbol.
    /// </summary>
    public static string FormatAmount(decimal amount, string currency)
    {
        var symbol = currency switch
        {
            "EUR" => "€",
            "USD" => "$",
            "GBP" => "£",
            _ => currency + " "
        };
        return $"{symbol}{amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Returns a deterministic CSS colour string for a given name.
    /// Used for group/family avatar backgrounds.
    /// </summary>
    public static string AvatarColor(string name)
    {
        var colors = new[]
        {
            "#4F46E5", "#7C3AED", "#DB2777", "#DC2626",
            "#D97706", "#059669", "#0891B2", "#0284C7",
        };
        return colors[Math.Abs(name.GetHashCode()) % colors.Length];
    }
}
