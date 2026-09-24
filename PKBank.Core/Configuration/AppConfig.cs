using System.Collections.Generic;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKBank.Core.Configuration;

public sealed class AppConfig
{
    /// <summary>
    ///     Schema version of the persisted payload, for migrations.
    /// </summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public string Language { get; set; } = GameLanguage.DefaultLanguage;

    public List<string> SavPaths { get; set; } = [];

    /// <summary>Folder holding one sub-folder per bank; empty means the default next to the config file.</summary>
    public string BanksPath { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class AppConfigJsonContext : JsonSerializerContext;
