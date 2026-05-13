using System.Text.Json;
using System.Text.Json.Serialization;

namespace WaterCueWindows.Models;

public class HydrationSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WaterCue", "settings.json");

    public int IntervalSeconds { get; set; } = 3600;
    public int WarningSeconds { get; set; } = 60;
    public int EmergencyDelaySeconds { get; set; } = 30;
    public int DailyGoal { get; set; } = 8;
    public bool WarnBeforeLock { get; set; } = true;
    public string GroqModel { get; set; } = "meta-llama/llama-4-scout-17b-16e-instruct";
    public string CameraDeviceId { get; set; } = string.Empty;
    public bool LaunchAtLogin { get; set; } = false;

    public static HydrationSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<HydrationSettings>(json) ?? new HydrationSettings();
            }
        }
        catch { }
        return new HydrationSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
