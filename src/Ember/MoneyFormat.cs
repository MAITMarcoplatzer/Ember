using System.Globalization;

namespace Ember;

public static class MoneyFormat
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    public static string Full(double value, string currency) => currency switch
    {
        "EUR" => value.ToString("N2", De) + " €",
        "USD" => "$" + value.ToString("N2", En),
        "GBP" => "£" + value.ToString("N2", En),
        _ => value.ToString("N2", De) + " " + currency,
    };

    public static string Compact(double value, string currency)
    {
        var rounded = Math.Round(value);
        var symbol = currency switch { "EUR" => "€", "USD" => "$", "GBP" => "£", _ => "" };
        return rounded < 100 ? rounded.ToString(De) + symbol : rounded.ToString(De);
    }

    /// <summary>Kompakte Token-Zahl: 980, 12,3K, 4,5M, 1,2Mrd.</summary>
    public static string Tokens(long value) => value switch
    {
        >= 1_000_000_000 => (value / 1_000_000_000d).ToString("0.#", De) + "Mrd",
        >= 1_000_000 => (value / 1_000_000d).ToString("0.#", De) + "M",
        >= 1_000 => (value / 1_000d).ToString("0.#", De) + "K",
        _ => value.ToString(De),
    };
}
