using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WaterCueWindows;

namespace WaterCueWindows.Views;

public partial class WarningWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _secondsRemaining;

    public WarningWindow()
    {
        InitializeComponent();

        _secondsRemaining = AppState.Shared.Settings.WarningSeconds;
        UpdateCountdownLabel();

        _timer.Tick += (_, _) =>
        {
            _secondsRemaining = Math.Max(0, _secondsRemaining - 1);
            UpdateCountdownLabel();
        };
        _timer.Start();

        KeyDown += OnKeyDown;
    }

    private void UpdateCountdownLabel()
    {
        CountdownLabel.Text = $"O Windows vai travar em {FormatSeconds(_secondsRemaining)}.";
    }

    private static string FormatSeconds(int s)
    {
        if (s >= 60) return $"{s / 60}min {s % 60:D2}s";
        return $"{s}s";
    }

    private void DrinkButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        AppState.Shared.DrinkNowFromWarning();
    }

    private void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        AppState.Shared.SnoozeWarning();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return || e.Key == Key.Enter)
        {
            e.Handled = true;
            DrinkButton_Click(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SnoozeButton_Click(sender, new RoutedEventArgs());
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
