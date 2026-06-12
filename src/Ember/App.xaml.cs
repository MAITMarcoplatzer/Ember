using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;
using WinForms = System.Windows.Forms;

namespace Ember;

public partial class App : Application
{
    public static AppSettings Settings { get; } = AppSettings.Load();

    private static Mutex? _singleInstance;

    private WinForms.NotifyIcon _tray = null!;
    private FlyoutWindow _flyout = null!;
    private DispatcherTimer _timer = null!;
    private Snapshot? _today;
    private Snapshot? _month;
    private bool _fetching;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--selftest-optimize"))
        {
            RunOptimizeSelftest();
            Shutdown();
            return;
        }

        _singleInstance = new Mutex(initiallyOwned: true, "EmberTrayApp", out var isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        _flyout = new FlyoutWindow();
        _flyout.RefreshRequested += () => _ = RefreshAsync();
        _flyout.SettingsChanged += OnSettingsChanged;

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Aktualisieren", null, (_, _) => _ = RefreshAsync());
        menu.Items.Add("Dashboard öffnen", null, (_, _) => OpenDashboard());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        var autostart = new WinForms.ToolStripMenuItem("Mit Windows starten")
        {
            CheckOnClick = true,
            Checked = Autostart.IsEnabled(),
        };
        autostart.CheckedChanged += (_, _) => Autostart.SetEnabled(autostart.Checked);
        menu.Items.Add(autostart);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => Shutdown());

        _tray = new WinForms.NotifyIcon
        {
            Icon = TrayIconFactory.CreatePlaceholder(),
            Text = "Ember – lädt…",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseUp += OnTrayMouseUp;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Settings.RefreshSeconds) };
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _timer.Start();

        _ = RefreshAsync();
    }

    private static void RunOptimizeSelftest()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ember-selftest.txt");
        try
        {
            // Task.Run vermeidet den Deadlock zwischen blockierendem UI-Thread
            // und Async-Fortsetzungen, die auf den Dispatcher zurückwollen
            var raw = Task.Run(() => CodeburnClient.RunRawAsync("optimize --period 30days"))
                .GetAwaiter().GetResult();
            var report = OptimizeReport.Parse(raw);
            var lines = new List<string>
            {
                $"Grade={report.Grade} Score={report.Score} Issues={report.Issues} TotalSavings={report.TotalSavings}",
                $"Findings={report.Findings.Count}",
            };
            lines.AddRange(report.Findings.Select(f =>
                $"  #{f.Rank} [{f.Severity}] {f.Title} | Savings={f.Savings} | Body={f.Body.Length} Zeichen | Action={(f.ActionText is null ? "-" : f.ActionText.Length + " Zeichen")}"));
            System.IO.File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText(path, "FEHLER: " + ex);
        }
    }

    private void OnSettingsChanged()
    {
        _timer.Interval = TimeSpan.FromSeconds(Settings.RefreshSeconds);
        UpdateTray();
    }

    private void OnTrayMouseUp(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Left) return;
        if ((DateTime.UtcNow - _flyout.LastHiddenUtc).TotalMilliseconds < 300) return;

        if (_flyout.IsVisible)
            _flyout.HideFlyout();
        else
            _flyout.ShowFlyout();
    }

    private async Task RefreshAsync()
    {
        if (_fetching) return;
        _fetching = true;
        try
        {
            var todayTask = CodeburnClient.FetchAsync("today");
            var monthTask = CodeburnClient.FetchAsync("month");
            await Task.WhenAll(todayTask, monthTask);
            _today = todayTask.Result;
            _month = monthTask.Result;

            UpdateTray();
            _flyout.SetData(_today, _month,
                "Stand " + DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("de-DE")));
        }
        catch (Exception ex)
        {
            _tray.Text = Truncate("Ember – Fehler: codeburn nicht erreichbar", 63);
            _flyout.SetData(_today, _month, "Fehler: " + Truncate(ex.Message, 80));
        }
        finally
        {
            _fetching = false;
        }
    }

    private void UpdateTray()
    {
        if (_today is null) return;

        var oldIcon = _tray.Icon;
        _tray.Icon = TrayIconFactory.CreateCostIcon(_today.Cost, _today.Currency, Settings.IconStyle);
        oldIcon?.Dispose();

        var tooltip = $"Ember · Heute {MoneyFormat.Full(_today.Cost, _today.Currency)}";
        if (_month is not null)
            tooltip += $" · Monat {MoneyFormat.Full(_month.Cost, _month.Currency)}";
        _tray.Text = Truncate(tooltip, 63);
    }

    private static void OpenDashboard() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
            "/c start \"CodeBurn\" cmd /k codeburn report")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _singleInstance?.ReleaseMutex();
        base.OnExit(e);
    }
}
