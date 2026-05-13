using System.IO;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using WaterCueWindows.Models;
using WaterCueWindows.Services;
using WaterCueWindows.Views;

namespace WaterCueWindows;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private TrayIconManager? _trayManager;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "watercue_debug.log");

    private static void Log(string msg)
        => File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, ex)
            => Log($"UNHANDLED: {ex.ExceptionObject}");
        DispatcherUnhandledException += (_, ex)
            => Log($"DISPATCHER: {ex.Exception}");

        Log("OnStartup begin");
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Log("ShutdownMode set");

        _trayIcon = (TaskbarIcon?)FindResource("TrayIcon");
        Log($"TrayIcon: {(_trayIcon == null ? "null" : "ok")}");

        Log("Injecting services");
        // Inject concrete services into AppState
        var state = AppState.Shared;
        state.TimerService     = TimerService.Shared;
        state.LockService      = LockService.Shared;
        state.CameraService    = CameraService.Shared;
        state.NotificationService = NotificationService.Shared;
        state.DatabaseService  = DatabaseService.Shared;
        state.SettingsService  = SettingsService.Shared;

        // NotificationService → balloon click = DrinkNow
        if (_trayIcon != null)
            NotificationService.Shared.Initialize(_trayIcon);
        NotificationService.Shared.OnDrinkNow = () =>
            Dispatcher.Invoke(state.DrinkNowFromWarning);

        // Timer events
        TimerService.Shared.OnWarning += (_, _) =>
            Dispatcher.Invoke(() =>
            {
                NotificationService.Shared.ScheduleWarning(state.Settings.WarningSeconds);
                if (state.Settings.WarnBeforeLock)
                    state.ShowWarningModal();
                else
                    state.EngageLock();
            });

        TimerService.Shared.OnLock += (_, _) =>
            Dispatcher.Invoke(() =>
            {
                state.DismissWarningModal();
                state.EngageLock();
                CameraService.Shared.Start();
            });

        // Camera → Groq → unlock
        CameraService.Shared.PhotoCaptured += async (_, jpeg) =>
        {
            try
            {
                var result = await GroqVisionService.Shared.ValidateAsync(jpeg, state.Settings.GroqModel);
                Dispatcher.Invoke(() =>
                {
                    if (result.Valid)
                    {
                        var evt = new HydrationEvent
                        {
                            ValidatedByAI = true,
                            GroqModel = state.Settings.GroqModel,
                            Reason = result.Reason
                        };
                        DatabaseService.Shared.Insert(evt);
                        state.Unlock();
                        state.RefreshStats();
                        NotificationService.Shared.SendUnlockCelebration();
                        TimerService.Shared.Schedule(state.Settings);
                    }
                    else
                    {
                        state.ValidationFailedReason = result.Reason;
                        state.LockState = LockState.ValidationFailed;
                    }
                });
            }
            catch (GroqException gex)
            {
                Dispatcher.Invoke(() =>
                {
                    state.ValidationFailedReason = gex.Message;
                    state.LockState = LockState.ValidationFailed;
                });
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    state.ValidationFailedReason = "Erro inesperado. Tente novamente.";
                    state.LockState = LockState.ValidationFailed;
                });
            }
        };

        Log("Creating TrayIconManager");
        // Tray icon manager (context menu + dynamic items)
        if (_trayIcon != null)
            _trayManager = new TrayIconManager(_trayIcon);

        Log($"NeedsOnboarding check");
        // Start or onboard
        bool onboard = NeedsOnboarding(state);
        Log($"NeedsOnboarding={onboard}");
        if (onboard)
        {
            Log("Showing OnboardingWindow");
            new OnboardingWindow().Show();
            Log("OnboardingWindow shown");
        }
        else
            StartApp(state);
        Log("OnStartup complete");
    }

    private static bool NeedsOnboarding(AppState state)
    {
        var flag = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WaterCue", ".onboarding_complete");
        return !File.Exists(flag) || string.IsNullOrEmpty(SettingsService.Shared.LoadApiKey());
    }

    public static void MarkOnboardingComplete()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WaterCue");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".onboarding_complete"), "1");
    }

    private static void StartApp(AppState state)
    {
        TimerService.Shared.Schedule(state.Settings);
        state.RefreshStats();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _trayIcon?.Dispose();
        CameraService.Shared.Stop();
        TimerService.Shared.CancelAll();
        base.OnExit(e);
    }
}
