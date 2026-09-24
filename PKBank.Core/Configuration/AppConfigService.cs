using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PKHeX.Core;

namespace PKBank.Core.Configuration;

public sealed class AppConfigService(IConfigStore store)
{
    private readonly AppConfig _config = Load(store);

    public event EventHandler? Changed;

    public string Language => _config.Language;
    public IReadOnlyList<string> SavPaths => _config.SavPaths;
    public string BanksPath => _config.BanksPath;

    public bool SetLanguage(string code)
    {
        if (!GameLanguage.IsLanguageValid(code) || code == _config.Language)
            return false;
        _config.Language = code;
        Commit();
        return true;
    }

    /// <returns>
    ///     <c>false</c> when the path is blank or already registered.
    /// </returns>
    public bool AddSavPath(string path) => Add(_config.SavPaths, path);

    /// <returns>
    ///     <c>false</c> when the path was not registered.
    /// </returns>
    public bool RemoveSavPath(string path) => Remove(_config.SavPaths, path);

    /// <summary>Sets the banks folder; blank goes back to the default one.</summary>
    /// <returns>
    ///     <c>false</c> when nothing changed.
    /// </returns>
    public bool SetBanksPath(string path)
    {
        var normalized = Normalize(path);
        if (normalized == _config.BanksPath)
            return false;
        _config.BanksPath = normalized;
        Commit();
        return true;
    }

    private bool Add(List<string> paths, string path)
    {
        var normalized = Normalize(path);
        if (normalized.Length == 0 || IndexOf(paths, normalized) >= 0)
            return false;
        paths.Add(normalized);
        Commit();
        return true;
    }

    private bool Remove(List<string> paths, string path)
    {
        var index = IndexOf(paths, Normalize(path));
        if (index < 0)
            return false;
        paths.RemoveAt(index);
        Commit();
        return true;
    }

    private static string Normalize(string path)
    {
        var trimmed = path.Trim();
        var stripped = trimmed.TrimEnd('/', '\\');
        return stripped.Length == 0 ? trimmed : stripped;
    }

    private static int IndexOf(List<string> paths, string normalized) =>
        paths.FindIndex(p => string.Equals(p, normalized, StringComparison.Ordinal));

    private void Commit()
    {
        try
        {
            store.Write(JsonSerializer.Serialize(_config, AppConfigJsonContext.Default.AppConfig));
        }
        catch
        {
            // Non-fatal: the change stays in memory, it just won't survive a restart.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppConfig Load(IConfigStore store)
    {
        try
        {
            var payload = store.Read();
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var loaded = JsonSerializer.Deserialize(payload, AppConfigJsonContext.Default.AppConfig);
                if (loaded is not null)
                    return Sanitize(loaded);
            }
        }
        catch
        {
            // Corrupt or unreadable configuration: fall back to defaults.
        }

        return new AppConfig();
    }

    private static AppConfig Sanitize(AppConfig config)
    {
        if (!GameLanguage.IsLanguageValid(config.Language))
            config.Language = GameLanguage.DefaultLanguage;

        Sanitize(config.SavPaths);
        config.BanksPath = Normalize(config.BanksPath ?? string.Empty);
        config.Version = AppConfig.CurrentVersion;
        return config;
    }

    private static void Sanitize(List<string> paths)
    {
        var kept = new List<string>(paths.Count);
        foreach (var normalized in paths.Select(Normalize).Where(p => p.Length != 0 && IndexOf(kept, p) < 0))
            kept.Add(normalized);

        paths.Clear();
        paths.AddRange(kept);
    }
}
