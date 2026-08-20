using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKBank.Desktop.Services;

/// <summary>
/// User settings, persisted as JSON in the platform config directory
/// (~/.config/PKBank.Desktop/settings.json on Linux).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Game data language code (see <see cref="GameLanguage"/>).</summary>
    public string Language { get; set; } = GameLanguage.DefaultLanguage;

    /// <summary>Version used for the blank save loaded at startup and offered by default.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<GameVersion>))]
    public GameVersion BlankSaveVersion { get; set; } = Latest.Version;

    /// <summary>Show shiny-variant sprites for shiny Pokémon.</summary>
    public bool ShinySprites { get; set; } = true;

    public static readonly IReadOnlyList<(string Code, string DisplayName)> Languages =
    [
        ("ja", "日本語"),
        ("en", "English"),
        ("fr", "Français"),
        ("it", "Italiano"),
        ("de", "Deutsch"),
        ("es", "Español"),
        ("es-419", "Español (Latinoamérica)"),
        ("ko", "한국어"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
    ];

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PKBank.Desktop", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath), Options);
                if (loaded is not null)
                {
                    if (!GameLanguage.IsLanguageValid(loaded.Language))
                        loaded.Language = GameLanguage.DefaultLanguage;
                    if (!loaded.BlankSaveVersion.IsValidSavedVersion())
                        loaded.BlankSaveVersion = Latest.Version;
                    return loaded;
                }
            }
        }
        catch
        {
            // Corrupt settings file: fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Non-fatal: settings just won't persist.
        }
    }
}
