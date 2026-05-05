using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Ramifier.Models;

namespace Ramifier.Services;

public class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ramifier");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public static void SetStartOnBoot(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue("Ramifier", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("Ramifier", throwOnMissingValue: false);
            }
        }
        catch { }
    }

    public static bool GetStartOnBoot()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Ramifier") != null;
        }
        catch { return false; }
    }
}
