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
}
