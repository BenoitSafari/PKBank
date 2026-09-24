using System;
using System.IO;
using PKBank.Core.Configuration;

namespace PKBank.Desktop.Utils;

public sealed class FileConfigStore : IConfigStore
{
    /// <summary>Repository folder holding the throwaway test saves and banks.</summary>
    private const string TestDataFolderName = ".data";

    private const string TestDataVariable = "USE_TEST_DATA";
    private const string FileName = "config.json";

    private static readonly string ConfigPath = Resolve();

    /// <summary>Folder holding the config file; the default banks folder sits next to it.</summary>
    public static string ConfigDirectory => Path.GetDirectoryName(ConfigPath)!;

    public string? Read() => File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : null;

    public void Write(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        var temp = ConfigPath + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, ConfigPath, true);
    }

    /// <summary>
    ///     <c>USE_TEST_DATA=1</c> walks up from the binary until a folder holding <c>.data</c> is found and uses
    ///     <c>.data/config.json</c>; any other value is taken as the data folder itself. Unset (or not found)
    ///     keeps the per-user location.
    /// </summary>
    private static string Resolve()
    {
        var requested = Environment.GetEnvironmentVariable(TestDataVariable);
        if (string.IsNullOrWhiteSpace(requested))
            return UserConfigPath;

        var folder = requested is "1" or "true" or "TRUE" ? FindTestDataFolder() : requested.Trim();
        return folder is null ? UserConfigPath : Path.Combine(folder, FileName);
    }

    private static string UserConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppInfo.Name, FileName);

    private static string? FindTestDataFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, TestDataFolderName);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
