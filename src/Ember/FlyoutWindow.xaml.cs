using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace Ember;

public partial class FlyoutWindow : Window
{
    private static readonly string[] Currencies = ["EUR", "USD", "GBP", "CHF"];
    private static readonly int[] Intervals = [15, 30, 60, 120];

    private Snapshot? _today;
    private Snapshot? _month;
    private IReadOnlyList<DailyCost>? _daily;
    private IReadOnlyList<double>? _hourly;
    private string _dailyCurrency = "EUR";
    private bool _showMonth;
    private bool _initializingSettings;
    private OptimizeReport? _optimizeCache;
    private DateTime _optimizeCacheTime;
    private bool _optimizeRunning;

    public event Action? RefreshRequested;
    public event Action? SettingsChanged;
    public DateTime LastHiddenUtc { get; private set; }

    public FlyoutWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => HideFlyout();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var preference = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
    }

    public void SetData(Snapshot? today, Snapshot? month, string status)
    {
        _today = today;
        _month = month;
        StatusText.Text = status;
        Render();
    }

    public void SetSparkline(IReadOnlyList<DailyCost> daily, string currency)
    {
        _daily = daily;
        _dailyCurrency = currency;
        RenderSparkline();
    }

    public void SetHourly(IReadOnlyList<double> hourly, string currency)
    {
        _hourly = hourly;
        _dailyCurrency = currency;
        RenderSparkline();
    }

    private void RenderSparkline()
    {
        if (_showMonth) RenderDailyBars();
        else RenderHourlyBars();
    }

    private void RenderDailyBars()
    {
        SparkGrid.Children.Clear();
        SparkGrid.ColumnDefinitions.Clear();

        if (_daily is null || _daily.Count == 0)
        {
            SparkLabels.Visibility = Visibility.Collapsed;
            return;
        }

        var de = CultureInfo.GetCultureInfo("de-DE");
        var max = Math.Max(_daily.Max(d => d.Cost), 0.0001);

        for (var i = 0; i < _daily.Count; i++)
        {
            var day = _daily[i];
            AddBar(i, day.Cost / max, highlight: i == _daily.Count - 1,
                $"{day.Date:dd.MM.}: {MoneyFormat.Full(day.Cost, _dailyCurrency)}");
        }

        SparkFromLabel.Text = _daily[0].Date.ToString("d. MMM", de);
        SparkToLabel.Text = "heute";
        SparkLabels.Visibility = Visibility.Visible;
        if (IsVisible) Reposition();
    }

    private void RenderHourlyBars()
    {
        SparkGrid.Children.Clear();
        SparkGrid.ColumnDefinitions.Clear();

        if (_hourly is null || _hourly.Count == 0)
        {
            SparkLabels.Visibility = Visibility.Collapsed;
            return;
        }

        var max = Math.Max(_hourly.Max(), 0.0001);
        var currentHour = DateTime.Now.Hour;

        for (var hour = 0; hour < _hourly.Count; hour++)
            AddBar(hour, _hourly[hour] / max, highlight: hour == currentHour,
                $"{hour}–{hour + 1} Uhr: {MoneyFormat.Full(_hourly[hour], _dailyCurrency)}");

        SparkFromLabel.Text = "0 Uhr";
        SparkToLabel.Text = "24 Uhr";
        SparkLabels.Visibility = Visibility.Visible;
        if (IsVisible) Reposition();
    }

    private void AddBar(int column, double fraction, bool highlight, string tooltip)
    {
        SparkGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rect = new Border
        {
            Background = Hex(highlight ? "#D85A30" : "#F0997B"),
            Opacity = highlight ? 1.0 : 0.55,
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(1, 0, 1, 0),
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = Math.Max(2, Math.Min(1, fraction) * SparkGrid.Height),
            ToolTip = tooltip,
        };
        Grid.SetColumn(rect, column);
        SparkGrid.Children.Add(rect);
    }

    public void ShowFlyout()
    {
        ApplyTheme();
        ShowView(MainPanel);
        Render();
        Show();
        Reposition();
        Activate();
    }

    public void HideFlyout()
    {
        if (!IsVisible) return;
        LastHiddenUtc = DateTime.UtcNow;
        Hide();
    }

    private void Reposition()
    {
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 12;
        Top = area.Bottom - ActualHeight - 12;
    }

    private void ShowView(StackPanel panel)
    {
        MainPanel.Visibility = panel == MainPanel ? Visibility.Visible : Visibility.Collapsed;
        OptimizePanel.Visibility = panel == OptimizePanel ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = panel == SettingsPanel ? Visibility.Visible : Visibility.Collapsed;
        if (IsVisible) Reposition();
    }

    // ----- Hauptansicht -------------------------------------------------

    private void Render()
    {
        var snap = _showMonth ? _month : _today;
        var palette = Theme.Current();

        TabToday.Background = _showMonth ? Brushes.Transparent : new SolidColorBrush(palette.Background);
        TabMonth.Background = _showMonth ? new SolidColorBrush(palette.Background) : Brushes.Transparent;

        if (snap is null)
        {
            BigText.Text = "–";
            SubText.Text = "Noch keine Daten geladen.";
            ModelsPanel.Children.Clear();
            ProjectsPanel.Children.Clear();
            return;
        }

        BigText.Text = MoneyFormat.Full(snap.Cost, snap.Currency);
        SubText.Text = string.Format(CultureInfo.GetCultureInfo("de-DE"),
            "{0:N0} Calls · {1} Sessions · Cache-Hit {2:N1} %",
            snap.Calls, snap.Sessions, snap.CacheHitPercent);

        RenderItems(ModelsPanel, snap.Models, snap);
        RenderItems(ProjectsPanel, snap.Projects, snap);
    }

    private void RenderItems(StackPanel panel, IReadOnlyList<UsageItem> items, Snapshot snap)
    {
        panel.Children.Clear();
        var palette = Theme.Current();
        var total = Math.Max(snap.Cost, 0.0001);

        foreach (var item in items.Take(3))
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });

            var name = new TextBlock
            {
                Text = item.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(palette.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(name, 0);

            var track = new Border
            {
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(palette.Surface),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var fillHost = new Grid();
            var fill = new Border
            {
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                Background = new SolidColorBrush(Palette.Bar),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            };
            fillHost.SizeChanged += (_, args) =>
                fill.Width = Math.Max(2, args.NewSize.Width * Math.Min(1, item.Cost / total));
            fillHost.Children.Add(fill);
            track.Child = fillHost;
            Grid.SetColumn(track, 1);

            var cost = new TextBlock
            {
                Text = MoneyFormat.Full(item.Cost, snap.Currency),
                FontSize = 12,
                Foreground = new SolidColorBrush(palette.Text),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(cost, 2);

            grid.Children.Add(name);
            grid.Children.Add(track);
            grid.Children.Add(cost);
            panel.Children.Add(grid);
        }
    }

    private void OnTabTodayClick(object sender, RoutedEventArgs e)
    {
        _showMonth = false;
        Render();
        RenderSparkline();
    }

    private void OnTabMonthClick(object sender, RoutedEventArgs e)
    {
        _showMonth = true;
        Render();
        RenderSparkline();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke();

    private void OnBackClick(object sender, RoutedEventArgs e) =>
        ShowView(MainPanel);

    private void OnDashboardClick(object sender, RoutedEventArgs e)
    {
        StartInTerminal("codeburn report");
        HideFlyout();
    }

    // ----- Export -------------------------------------------------------

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var period = _showMonth ? "monat" : "heute";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "CodeBurn-Daten exportieren",
            FileName = $"codeburn-{period}-{DateTime.Now:yyyy-MM-dd}",
            DefaultExt = ".csv",
            Filter = "CSV-Datei (*.csv)|*.csv|JSON-Datei (*.json)|*.json",
        };
        if (dialog.ShowDialog(this) != true) return;

        var format = dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "json" : "csv";
        var from = _showMonth
            ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)
            : DateTime.Now.Date;

        StatusText.Text = "Exportiere…";
        try
        {
            await CodeburnClient.RunRawAsync(
                $"export -f {format} -o \"{dialog.FileName}\" " +
                $"--from {from:yyyy-MM-dd} --to {DateTime.Now:yyyy-MM-dd}");
            StatusText.Text = "Export gespeichert";
            Process.Start(new ProcessStartInfo("explorer.exe",
                $"/select,\"{dialog.FileName}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = "Export fehlgeschlagen: " + Shorten(ex.Message, 60);
        }
    }

    // ----- Optimize -----------------------------------------------------

    private async void OnOptimizeClick(object sender, RoutedEventArgs e)
    {
        ShowView(OptimizePanel);

        if (_optimizeCache is not null && DateTime.UtcNow - _optimizeCacheTime < TimeSpan.FromMinutes(10))
        {
            RenderOptimize(_optimizeCache);
            return;
        }
        if (_optimizeRunning) return;

        _optimizeRunning = true;
        OptScoreRow.Visibility = Visibility.Collapsed;
        OptFindingsPanel.Children.Clear();
        OptMoreText.Text = "";
        OptStatusText.Text = "Analysiere Sessions… (dauert einen Moment)";
        OptStatusText.Visibility = Visibility.Visible;

        try
        {
            var raw = await CodeburnClient.RunRawAsync("optimize --period 30days");
            _optimizeCache = OptimizeReport.Parse(raw);
            _optimizeCacheTime = DateTime.UtcNow;
            RenderOptimize(_optimizeCache);
        }
        catch (Exception ex)
        {
            OptStatusText.Text = "Analyse fehlgeschlagen: " + Shorten(ex.Message, 80);
        }
        finally
        {
            _optimizeRunning = false;
        }
    }

    private void RenderOptimize(OptimizeReport report)
    {
        var light = Theme.IsLight();
        OptStatusText.Visibility = Visibility.Collapsed;
        OptScoreRow.Visibility = Visibility.Visible;

        var (healthBg, healthFg) = report.Score switch
        {
            >= 80 => light ? ("#E1F5EE", "#085041") : ("#085041", "#9FE1CB"),
            >= 50 => light ? ("#FAEEDA", "#633806") : ("#633806", "#FAC775"),
            _ => light ? ("#FCEBEB", "#791F1F") : ("#501313", "#F7C1C1"),
        };
        StyleCard(OptHealthCard, OptHealthLabel, OptHealthValue, healthBg, healthFg);
        OptHealthValue.Text = report.Grade is null
            ? "–"
            : $"{report.Grade}  ({report.Score}/100)";

        var (saveBg, saveFg) = light ? ("#E1F5EE", "#085041") : ("#085041", "#9FE1CB");
        StyleCard(OptSaveCard, OptSaveLabel, OptSaveValue, saveBg, saveFg);
        OptSaveValue.Text = FormatSavings(report.TotalSavings) ?? "–";

        OptFindingsPanel.Children.Clear();
        foreach (var finding in report.Findings.Take(3))
            OptFindingsPanel.Children.Add(BuildFindingCard(finding));

        var more = report.Findings.Count - 3;
        OptMoreText.Text = more > 0 ? $"{more} weitere Befunde" : "";
        if (IsVisible) Reposition();
    }

    private Border BuildFindingCard(OptimizeFinding finding)
    {
        var palette = Theme.Current();
        var light = Theme.IsLight();

        var (sevText, sevBg, sevFg) = finding.Severity switch
        {
            "High" => ("Hoch", light ? "#FCEBEB" : "#501313", light ? "#A32D2D" : "#F7C1C1"),
            "Medium" => ("Mittel", light ? "#FAEEDA" : "#412402", light ? "#854F0B" : "#FAC775"),
            _ => ("Niedrig", light ? "#ECECEC" : "#3A3A3A", light ? "#5F5E5A" : "#B4B2A9"),
        };

        var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var chip = new Border
        {
            Background = Hex(sevBg),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 1, 7, 1),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = sevText, FontSize = 10, Foreground = Hex(sevFg) },
        };
        Grid.SetColumn(chip, 0);

        var titleBlock = new TextBlock
        {
            Text = finding.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(palette.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(titleBlock, 1);

        var savingsBlock = new TextBlock
        {
            Text = FormatSavings(finding.Savings) ?? "",
            FontSize = 12,
            Foreground = Hex(Theme.IsLight() ? "#0F6E56" : "#5DCAA5"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(savingsBlock, 2);

        header.Children.Add(chip);
        header.Children.Add(titleBlock);
        header.Children.Add(savingsBlock);

        var content = new StackPanel();
        content.Children.Add(header);
        content.Children.Add(new TextBlock
        {
            Text = Shorten(finding.Body, 160),
            FontSize = 11,
            Foreground = new SolidColorBrush(palette.TextMuted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, finding.ActionText is null ? 0 : 7),
        });

        if (finding.ActionText is not null)
        {
            var copyButton = new Button
            {
                Style = (Style)FindResource("FlatButton"),
                Padding = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Text = "Fix kopieren",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(palette.Text),
                },
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(palette.Surface),
            };
            copyButton.Click += (_, _) =>
            {
                Clipboard.SetText(finding.ActionText);
                ((TextBlock)copyButton.Content).Text = "Kopiert ✓";
            };
            content.Children.Add(copyButton);
        }

        return new Border
        {
            BorderBrush = new SolidColorBrush(palette.Border),
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 8),
            Child = content,
        };
    }

    private void OnOptimizeTerminalClick(object sender, RoutedEventArgs e)
    {
        StartInTerminal("codeburn optimize --period 30days");
        HideFlyout();
    }

    private static string? FormatSavings(string? raw)
    {
        if (raw is null) return null;
        var match = Regex.Match(raw.Trim(), @"^([€$£])\s*([\d.,]+)$");
        if (!match.Success) return raw;
        if (!double.TryParse(match.Groups[2].Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var value)) return raw;
        var currency = match.Groups[1].Value switch
        {
            "€" => "EUR", "$" => "USD", "£" => "GBP", _ => "EUR",
        };
        return MoneyFormat.Full(value, currency);
    }

    // ----- Einstellungen ------------------------------------------------

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _initializingSettings = true;

        CmbCurrency.ItemsSource = Currencies;
        CmbCurrency.SelectedItem = _today?.Currency is { } c && Currencies.Contains(c) ? c : "EUR";

        CmbInterval.ItemsSource = Intervals.Select(i => $"alle {i} Sekunden").ToList();
        var idx = Array.IndexOf(Intervals, App.Settings.RefreshSeconds);
        CmbInterval.SelectedIndex = idx >= 0 ? idx : 1;

        CmbIconStyle.ItemsSource = new[] { "Tagesbetrag", "Flamme" };
        CmbIconStyle.SelectedIndex = App.Settings.IconStyle == "flame" ? 1 : 0;

        ChkAutostart.IsChecked = Autostart.IsEnabled();
        SettingsStatusText.Text = "Änderungen werden sofort übernommen.";

        _initializingSettings = false;
        ShowView(SettingsPanel);
    }

    private async void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || CmbCurrency.SelectedItem is not string code) return;
        SettingsStatusText.Text = $"Stelle Währung auf {code} um…";
        try
        {
            await CodeburnClient.RunRawAsync($"currency {code}");
            SettingsStatusText.Text = $"Währung: {code} – Daten werden neu geladen.";
            SettingsChanged?.Invoke();
            RefreshRequested?.Invoke();
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = "Fehler: " + Shorten(ex.Message, 60);
        }
    }

    private void OnIntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || CmbInterval.SelectedIndex < 0) return;
        App.Settings.RefreshSeconds = Intervals[CmbInterval.SelectedIndex];
        App.Settings.Save();
        SettingsChanged?.Invoke();
        SettingsStatusText.Text = $"Aktualisierung alle {App.Settings.RefreshSeconds} Sekunden.";
    }

    private void OnIconStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingSettings || CmbIconStyle.SelectedIndex < 0) return;
        App.Settings.IconStyle = CmbIconStyle.SelectedIndex == 1 ? "flame" : "amount";
        App.Settings.Save();
        SettingsChanged?.Invoke();
        SettingsStatusText.Text = "Tray-Icon aktualisiert.";
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_initializingSettings) return;
        Autostart.SetEnabled(ChkAutostart.IsChecked == true);
        SettingsStatusText.Text = ChkAutostart.IsChecked == true
            ? "Ember startet künftig mit Windows."
            : "Autostart deaktiviert.";
    }

    // ----- Hilfsfunktionen ----------------------------------------------

    private void ApplyTheme()
    {
        var p = Theme.Current();
        SetBrush("BgBrush", p.Background);
        SetBrush("TextBrush", p.Text);
        SetBrush("MutedBrush", p.TextMuted);
        SetBrush("SurfaceBrush", p.Surface);
        SetBrush("EdgeBrush", p.Border);
        SetBrush("AccentBrush", Palette.Accent);
    }

    private void SetBrush(string key, Color color) =>
        Resources[key] = new SolidColorBrush(color);

    private static void StyleCard(Border card, TextBlock label, TextBlock value, string bg, string fg)
    {
        card.Background = Hex(bg);
        label.Foreground = Hex(fg);
        value.Foreground = Hex(fg);
    }

    private static SolidColorBrush Hex(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    private static string Shorten(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private static void StartInTerminal(string command) =>
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c start \"CodeBurn\" cmd /k {command}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
