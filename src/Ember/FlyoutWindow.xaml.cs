using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace Ember;

public partial class FlyoutWindow : Window
{
    private Snapshot? _today;
    private Snapshot? _month;
    private bool _showMonth;

    public event Action? RefreshRequested;
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

    public void ShowFlyout()
    {
        ApplyTheme();
        Render();
        Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 12;
        Top = area.Bottom - ActualHeight - 12;
        Activate();
    }

    public void HideFlyout()
    {
        if (!IsVisible) return;
        LastHiddenUtc = DateTime.UtcNow;
        Hide();
    }

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

    private void OnTabTodayClick(object sender, RoutedEventArgs e)
    {
        _showMonth = false;
        Render();
    }

    private void OnTabMonthClick(object sender, RoutedEventArgs e)
    {
        _showMonth = true;
        Render();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke();

    private void OnDashboardClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("cmd.exe",
            "/c start \"CodeBurn\" cmd /k codeburn report")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        HideFlyout();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
