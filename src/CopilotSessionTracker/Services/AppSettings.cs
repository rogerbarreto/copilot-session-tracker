using System;
using System.IO;
using System.Text.Json;

namespace CopilotSessionTracker.Services;

/// <summary>
/// Simple JSON-backed settings persisted to
/// <c>%LOCALAPPDATA%\CopilotSessionTracker\settings.json</c>. Used instead of
/// <c>Windows.Storage.ApplicationData</c> because this app runs unpackaged
/// (WindowsPackageType=None), where ApplicationData has no identity.
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CopilotSessionTracker",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// The command template used by the Terminal button. Supports the tokens
    /// <c>{id}</c> (session id) and <c>{cwd}</c> (working directory); everything else is
    /// passed through verbatim, so users can add flags such as <c>--yolo</c> or
    /// <c>--prefer-version &lt;v&gt;</c>.
    /// </summary>
    public string CommandTemplate { get; set; } = TerminalLauncher.DefaultCommandTemplate;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.CommandTemplate))
                    {
                        loaded.CommandTemplate = TerminalLauncher.DefaultCommandTemplate;
                    }

                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to defaults on any read/parse error.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception)
        {
            // Persistence is best-effort; ignore failures.
        }
    }
}
