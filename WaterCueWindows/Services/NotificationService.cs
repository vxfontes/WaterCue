using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace WaterCueWindows.Services;

public class NotificationService
{
    public static readonly NotificationService Shared = new();

    public Action? OnDrinkNow { get; set; }

    private TaskbarIcon? _trayIcon;

    private NotificationService() { }

    public void Initialize(TaskbarIcon icon)
    {
        _trayIcon = icon;
        _trayIcon.TrayBalloonTipClicked += OnBalloonClicked;
    }

    // Schedules a warning balloon immediately (WPF balloons don't support delay scheduling)
    public void ScheduleWarning(int secondsUntilLock)
    {
        var minutes = Math.Max(1, secondsUntilLock / 60);
        ShowBalloon("WaterCue ⚡", $"Beba água agora — tela trava em {minutes} minuto(s)!");
    }

    public void CancelWarning()
    {
        // Balloon tips can't be cancelled once shown; no-op.
    }

    public void SendUnlockCelebration()
    {
        ShowBalloon("WaterCue ✅", "Boa! Hidratação validada. Até a próxima.");
    }

    private void ShowBalloon(string title, string message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var icon = _trayIcon ?? TryGetTrayIcon();
            icon?.ShowBalloonTip(title, message, BalloonIcon.Info);
        });
    }

    private static TaskbarIcon? TryGetTrayIcon()
        => Application.Current?.Resources["TrayIcon"] as TaskbarIcon;

    private void OnBalloonClicked(object? sender, RoutedEventArgs e)
    {
        OnDrinkNow?.Invoke();
    }
}
