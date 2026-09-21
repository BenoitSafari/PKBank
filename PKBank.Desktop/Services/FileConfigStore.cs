using System;
using System.IO;
using PKBank.Core.Configuration;

namespace PKBank.Desktop.Services;

public sealed class FileConfigStore : IConfigStore
{
    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PKBank.Desktop", "config.json");

    public string? Read() => File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : null;

    public void Write(string content)
    {
        var path = ConfigPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }
}
