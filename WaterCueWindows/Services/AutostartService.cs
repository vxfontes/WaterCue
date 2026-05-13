using Microsoft.Win32;

namespace WaterCueWindows.Services;

public class AutostartService
{
    public static readonly AutostartService Shared = new();

    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WaterCue";

    private AutostartService() { }

    public bool IsRegistered
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
    }

    public void Register()
    {
        var exePath = GetExePath();
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true)
            ?? throw new InvalidOperationException("Cannot open Run registry key.");
        key.SetValue(AppName, $"\"{exePath}\"");
    }

    public void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string GetExePath()
        => System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
           ?? throw new InvalidOperationException("Cannot determine executable path.");
}
