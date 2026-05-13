using System.IO;
using System.Security.Cryptography;
using System.Text;
using WaterCueWindows.Models;

namespace WaterCueWindows.Services;

public class SettingsService
{
    public static readonly SettingsService Shared = new();

    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WaterCue");

    private static readonly string EncryptedKeyPath = Path.Combine(AppDataPath, "apikey.bin");

    private SettingsService() { }

    public HydrationSettings Load() => HydrationSettings.Load();

    public void Save(HydrationSettings settings) => settings.Save();

    public void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(AppDataPath);
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(EncryptedKeyPath, encrypted);
    }

    public string? LoadApiKey()
    {
        try
        {
            if (!File.Exists(EncryptedKeyPath)) return null;
            var encrypted = File.ReadAllBytes(EncryptedKeyPath);
            var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }

    public void DeleteApiKey()
    {
        if (File.Exists(EncryptedKeyPath))
            File.Delete(EncryptedKeyPath);
    }
}
