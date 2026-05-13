using System.IO;
using System.Text.Json;
using WaterCueWindows.Models;

namespace WaterCueWindows.Services;

public class TimerService
{
    private static readonly string StateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) is { Length: > 0 } appData
            ? appData
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "WaterCue");

    private static readonly string StatePath = Path.Combine(StateDir, "timer_state.json");

    public static readonly TimerService Shared = new();

    public event EventHandler? OnWarning;
    public event EventHandler? OnLock;
    public event EventHandler? CountdownTick;

    public DateTime NextLockDate { get; private set; } = DateTime.Now;

    public TimeSpan TimeUntilLock => NextLockDate > DateTime.Now
        ? NextLockDate - DateTime.Now
        : TimeSpan.Zero;

    private System.Timers.Timer? _warningTimer;
    private System.Timers.Timer? _lockTimer;
    private System.Timers.Timer? _countdownTimer;

    private TimerService()
    {
        RestoreOrSchedule();
    }

    public void Schedule(HydrationSettings settings)
    {
        CancelAll();
        var nextLock = DateTime.Now.AddSeconds(settings.IntervalSeconds);
        NextLockDate = nextLock;
        PersistNextLock(nextLock);
        Arm(settings, nextLock);
        StartCountdown();
    }

    public void Reset(HydrationSettings settings) => Schedule(settings);

    // Called by AppState with a single int: 300 + Settings.WarningSeconds
    public void Snooze(int extraSeconds)
    {
        var settings = HydrationSettings.Load();
        CancelAll();
        var newLockDate = NextLockDate.AddSeconds(extraSeconds);
        NextLockDate = newLockDate;
        PersistNextLock(newLockDate);
        Arm(settings, newLockDate);
        StartCountdown();
    }

    public void CancelAll()
    {
        DisposeTimer(ref _warningTimer);
        DisposeTimer(ref _lockTimer);
        DisposeTimer(ref _countdownTimer);
    }

    public string TimeUntilLockFormatted
    {
        get
        {
            var t = (int)TimeUntilLock.TotalSeconds;
            if (t <= 0) return "Agora";
            var h = t / 3600;
            var m = (t % 3600) / 60;
            var s = t % 60;
            if (h > 0) return $"{h}h {m:D2}m";
            if (m > 0) return $"{m}m {s:D2}s";
            return $"{s}s";
        }
    }

    private void RestoreOrSchedule()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var json = File.ReadAllText(StatePath);
                using var doc = JsonDocument.Parse(json);
                var epoch = doc.RootElement.GetProperty("nextLock").GetDouble();
                var restored = DateTimeOffset.FromUnixTimeMilliseconds((long)(epoch * 1000)).LocalDateTime;
                if (restored > DateTime.Now)
                {
                    NextLockDate = restored;
                    var settings = HydrationSettings.Load();
                    Arm(settings, restored);
                    StartCountdown();
                    return;
                }
            }
        }
        catch { }

        Schedule(HydrationSettings.Load());
    }

    private void PersistNextLock(DateTime date)
    {
        try
        {
            Directory.CreateDirectory(StateDir);
            var epoch = new DateTimeOffset(date).ToUnixTimeMilliseconds() / 1000.0;
            File.WriteAllText(StatePath, $"{{\"nextLock\":{epoch}}}");
        }
        catch { }
    }

    private void Arm(HydrationSettings settings, DateTime nextLock)
    {
        var now = DateTime.Now;
        var lockDelay = (nextLock - now).TotalMilliseconds;

        if (lockDelay <= 0)
        {
            OnLock?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (settings.WarnBeforeLock)
        {
            var warnDelay = lockDelay - settings.WarningSeconds * 1000.0;
            if (warnDelay > 0)
            {
                _warningTimer = new System.Timers.Timer(warnDelay) { AutoReset = false };
                _warningTimer.Elapsed += (_, _) => OnWarning?.Invoke(this, EventArgs.Empty);
                _warningTimer.Start();
            }
        }

        _lockTimer = new System.Timers.Timer(lockDelay) { AutoReset = false };
        _lockTimer.Elapsed += (_, _) => OnLock?.Invoke(this, EventArgs.Empty);
        _lockTimer.Start();
    }

    private void StartCountdown()
    {
        DisposeTimer(ref _countdownTimer);
        _countdownTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _countdownTimer.Elapsed += (_, _) => CountdownTick?.Invoke(this, EventArgs.Empty);
        _countdownTimer.Start();
    }

    private static void DisposeTimer(ref System.Timers.Timer? timer)
    {
        timer?.Stop();
        timer?.Dispose();
        timer = null;
    }
}
