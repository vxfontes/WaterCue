using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WaterCueWindows.Models;
using WaterCueWindows.Services;

namespace WaterCueWindows;

public enum LockState
{
    Idle,
    Warning,
    Locked,
    Validating,
    ValidationFailed
}

public class AppState : INotifyPropertyChanged
{
    public static AppState Shared { get; } = new AppState();

    private AppState() { }

    // --- Service references (injected by App.xaml.cs) ---
    public TimerService? TimerService { get; set; }
    public LockService? LockService { get; set; }
    public CameraService? CameraService { get; set; }
    public NotificationService? NotificationService { get; set; }
    public DatabaseService? DatabaseService { get; set; }
    public SettingsService? SettingsService { get; set; }

    // --- Observable properties ---

    private LockState _lockState = LockState.Idle;
    public LockState LockState
    {
        get => _lockState;
        set { _lockState = value; OnPropertyChanged(); }
    }

    private string _validationFailedReason = string.Empty;
    public string ValidationFailedReason
    {
        get => _validationFailedReason;
        set { _validationFailedReason = value; OnPropertyChanged(); }
    }

    private HydrationSettings _settings = HydrationSettings.Load();
    public HydrationSettings Settings
    {
        get => _settings;
        set { _settings = value; OnPropertyChanged(); }
    }

    private int _cupsToday;
    public int CupsToday
    {
        get => _cupsToday;
        set { _cupsToday = value; OnPropertyChanged(); }
    }

    private int _currentStreak;
    public int CurrentStreak
    {
        get => _currentStreak;
        set { _currentStreak = value; OnPropertyChanged(); }
    }

    private Window? _warningWindow;

    // --- Warning cycle ---

    public void ShowWarningModal()
    {
        if (_warningWindow != null) return;
        LockState = LockState.Warning;
        var winType = Type.GetType("WaterCueWindows.Views.WarningWindow, WaterCueWindows");
        if (winType != null)
        {
            _warningWindow = (Window?)Activator.CreateInstance(winType);
            _warningWindow?.Show();
        }
    }

    public void DismissWarningModal()
    {
        _warningWindow?.Close();
        _warningWindow = null;
    }

    public void DrinkNowFromWarning()
    {
        DismissWarningModal();
        EngageLock();
    }

    public void SnoozeWarning()
    {
        DismissWarningModal();
        LockState = LockState.Idle;
        TimerService?.Snooze(300 + Settings.WarningSeconds);

        Task.Delay(TimeSpan.FromSeconds(300)).ContinueWith(_ =>
            Application.Current.Dispatcher.Invoke(ShowWarningModal));
    }

    // --- Lock cycle ---

    public void EngageLock()
    {
        LockState = LockState.Locked;
        LockService?.Engage();
        CameraService?.Start();
    }

    public void CaptureAndValidate()
    {
        LockState = LockState.Validating;
        CameraService?.CapturePhoto();
    }

    public void EmergencyUnlock()
    {
        DatabaseService?.Insert(new HydrationEvent
        {
            ValidatedByAI = false,
            EmergencyBypass = true,
            Reason = "Emergency bypass"
        });
        Unlock();
        RefreshStats();
    }

    public void Unlock()
    {
        LockState = LockState.Idle;
        CameraService?.Stop();
        LockService?.Release();
    }

    public void RefreshStats()
    {
        if (DatabaseService == null) return;
        CupsToday = DatabaseService.CupsToday();
        CurrentStreak = DatabaseService.CurrentStreak(Settings.DailyGoal);
    }

    // --- INotifyPropertyChanged ---

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
