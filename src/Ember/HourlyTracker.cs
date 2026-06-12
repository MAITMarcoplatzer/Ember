using System.IO;
using System.Text.Json;

namespace Ember;

/// <summary>
/// Leitet Stundenkosten aus dem kumulierten Tageswert ab, den die App ohnehin
/// alle 30 Sekunden abfragt — die CLI selbst liefert nur Tagesgranularität.
/// Stunden ohne laufende Ember-Instanz bleiben leer.
/// </summary>
public sealed class HourlyTracker
{
    private sealed class State
    {
        public string Date { get; set; } = "";
        public string Currency { get; set; } = "";
        public double? LastCost { get; set; }
        public double[] Hours { get; set; } = new double[24];
    }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Ember", "hourly.json");

    private State _state = LoadState();

    public IReadOnlyList<double> Hours => _state.Hours;

    public void Sample(double todayCost, string currency, DateTime now)
    {
        var today = now.ToString("yyyy-MM-dd");

        if (_state.Date != today || _state.Currency != currency)
        {
            // Neuer Tag oder Währungswechsel: Basislinie neu setzen, ohne die
            // bis dahin aufgelaufenen Kosten einer falschen Stunde zuzuschlagen
            _state = new State { Date = today, Currency = currency, LastCost = todayCost };
            Save();
            return;
        }

        if (_state.LastCost is { } last && todayCost > last)
            _state.Hours[now.Hour] += todayCost - last;

        _state.LastCost = todayCost;
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_state));
        }
        catch
        {
            // Persistenz ist Komfort, kein Muss
        }
    }

    private static State LoadState()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<State>(File.ReadAllText(FilePath)) ?? new State();
        }
        catch
        {
            // korrupte Datei -> frisch starten
        }
        return new State();
    }
}
