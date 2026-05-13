using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using WaterCueWindows;
using WaterCueWindows.Services;

namespace WaterCueWindows.Views;

public sealed class TrayIconManager : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly DispatcherTimer _refreshTimer;

    private MenuItem _nextLockItem = null!;
    private MenuItem _cupsItem = null!;

    public TrayIconManager(TaskbarIcon trayIcon)
    {
        _trayIcon = trayIcon;
        BuildContextMenu();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _refreshTimer.Tick += (_, _) => RefreshDynamicItems();
        _refreshTimer.Start();

        AppState.Shared.PropertyChanged += OnAppStateChanged;
        RefreshDynamicItems();
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Style = null;

        _nextLockItem = new MenuItem { IsEnabled = false, FontSize = 13 };
        _cupsItem = new MenuItem { IsEnabled = false, FontSize = 13 };

        var drinkNow = new MenuItem
        {
            Header = "💧  Beber agora",
            FontSize = 13
        };
        drinkNow.Click += (_, _) => AppState.Shared.EngageLock();

        var stats = new MenuItem { Header = "📊  Estatísticas", FontSize = 13 };
        stats.Click += (_, _) => OpenWindow<StatsWindow>();

        var settings = new MenuItem { Header = "⚙  Configurações", FontSize = 13 };
        settings.Click += (_, _) => OpenWindow<SettingsWindow>();

        var quit = new MenuItem { Header = "Sair", FontSize = 13 };
        quit.Click += (_, _) => Application.Current.Shutdown();

        menu.Items.Add(_nextLockItem);
        menu.Items.Add(_cupsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(drinkNow);
        menu.Items.Add(new Separator());
        menu.Items.Add(stats);
        menu.Items.Add(settings);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);

        _trayIcon.ContextMenu = menu;
        _trayIcon.DoubleClickCommand = new RelayCommand(_ => OpenWindow<StatsWindow>());
    }

    private void RefreshDynamicItems()
    {
        string timeText = TimerService.Shared.TimeUntilLockFormatted;
        _nextLockItem.Header = $"🕐  Próximo lock: {timeText}";
        _cupsItem.Header = $"💧  Copos hoje: {AppState.Shared.CupsToday} / {AppState.Shared.Settings.DailyGoal}";
    }

    private void OnAppStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.CupsToday) or nameof(AppState.Settings))
            Application.Current.Dispatcher.Invoke(RefreshDynamicItems);
    }

    private static void OpenWindow<T>() where T : Window, new()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is T) { w.Activate(); return; }
        }
        new T().Show();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        AppState.Shared.PropertyChanged -= OnAppStateChanged;
    }
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
